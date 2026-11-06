using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class RoutingSlipGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKRS001",
        "Routing slip type must be partial",
        "Type '{0}' is marked with [GenerateRoutingSlip] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingSteps = new(
        "PKRS002",
        "Routing slip has no steps",
        "Type '{0}' is marked with [GenerateRoutingSlip] but does not declare any [RoutingSlipStep] methods",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidStep = new(
        "PKRS003",
        "Routing slip step signature is invalid",
        "Routing slip step '{0}' must be static and return Message<TPayload> or ValueTask<Message<TPayload>> with the required message/context parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateRoutingSlipAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateRoutingSlipAttribute");
            if (attr is null)
                return;

            Generate(spc, candidate.Type, candidate.Node, attr);
        });
    }

    private static void Generate(
        SourceProductionContext context,
        INamedTypeSymbol type,
        TypeDeclarationSyntax node,
        AttributeData attribute)
    {
        if (!node.Modifiers.Any(static modifier => modifier.Text == "partial"))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var payloadType = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (payloadType is null)
            return;

        var hasStepAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.RoutingSlipStepAttribute"));
        var steps = GetSteps(type, payloadType, context);
        if (steps.Length == 0)
        {
            if (!hasStepAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingSteps, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var syncSteps = steps.Where(static step => !step.IsAsync).OrderBy(static step => step.Order).ThenBy(static step => step.Name).ToArray();
        var asyncSteps = steps.Where(static step => step.IsAsync).OrderBy(static step => step.Order).ThenBy(static step => step.Name).ToArray();
        var config = new RoutingSlipConfig(
            GetNamedString(attribute, "FactoryName") ?? "Create",
            GetNamedString(attribute, "AsyncFactoryName") ?? "CreateAsync");

        context.AddSource($"{type.Name}.RoutingSlip.g.cs", SourceText.From(GenerateSource(type, payloadType, syncSteps, asyncSteps, config), Encoding.UTF8));
    }

    private static ImmutableArray<RoutingStep> GetSteps(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<RoutingStep>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.RoutingSlipStepAttribute");
            if (attr is null)
                continue;

            if (!TryGetStep(method, payloadType, attr, out var step))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidStep, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(step);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetStep(
        IMethodSymbol method,
        INamedTypeSymbol payloadType,
        AttributeData attribute,
        out RoutingStep step)
    {
        step = default;
        if (!method.IsStatic || attribute.ConstructorArguments.Length != 2)
            return false;

        var name = attribute.ConstructorArguments[0].Value as string;
        var order = attribute.ConstructorArguments[1].Value as int? ?? 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var syncReturn = IsMessageOfPayload(method.ReturnType, payloadType);
        var asyncReturn = IsValueTaskOfMessage(method.ReturnType, payloadType);
        if (!syncReturn && !asyncReturn)
            return false;

        if (syncReturn && method.Parameters.Length == 2 &&
            IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
            method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext")
        {
            step = new RoutingStep(name!, order, method.Name, isAsync: false);
            return true;
        }

        if (asyncReturn && method.Parameters.Length == 3 &&
            IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
            method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
            method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken")
        {
            step = new RoutingStep(name!, order, method.Name, isAsync: true);
            return true;
        }

        return false;
    }

    private static bool IsMessageOfPayload(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool IsValueTaskOfMessage(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>" &&
           named.TypeArguments.Length == 1 &&
           IsMessageOfPayload(named.TypeArguments[0], payloadType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        IReadOnlyList<RoutingStep> syncSteps,
        IReadOnlyList<RoutingStep> asyncSteps,
        RoutingSlipConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("partial ").Append(GetKind(type)).Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        if (syncSteps.Count > 0)
        {
            sb.Append("    public static global::PatternKit.Messaging.Routing.RoutingSlip<")
                .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("> ")
                .Append(config.FactoryName)
                .AppendLine("()");
            sb.AppendLine("        => global::PatternKit.Messaging.Routing.RoutingSlip<" + payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">.Create()");
            foreach (var step in syncSteps)
                sb.Append("            .Step(\"").Append(Escape(step.Name)).Append("\", ").Append(step.MethodName).AppendLine(")");
            sb.AppendLine("            .Build();");
            sb.AppendLine();
        }

        if (asyncSteps.Count > 0)
        {
            sb.Append("    public static global::PatternKit.Messaging.Routing.AsyncRoutingSlip<")
                .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("> ")
                .Append(config.AsyncFactoryName)
                .AppendLine("()");
            sb.AppendLine("        => global::PatternKit.Messaging.Routing.AsyncRoutingSlip<" + payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">.Create()");
            foreach (var step in asyncSteps)
                sb.Append("            .Step(\"").Append(Escape(step.Name)).Append("\", ").Append(step.MethodName).AppendLine(")");
            sb.AppendLine("            .Build();");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
            if (argument.Key == name)
                return argument.Value.Value as string;

        return null;
    }

    private readonly struct RoutingStep
    {
        public RoutingStep(string name, int order, string methodName, bool isAsync)
            => (Name, Order, MethodName, IsAsync) = (name, order, methodName, isAsync);

        public string Name { get; }

        public int Order { get; }

        public string MethodName { get; }

        public bool IsAsync { get; }
    }

    private readonly struct RoutingSlipConfig
    {
        public RoutingSlipConfig(string factoryName, string asyncFactoryName)
            => (FactoryName, AsyncFactoryName) = (factoryName, asyncFactoryName);

        public string FactoryName { get; }

        public string AsyncFactoryName { get; }
    }
}
