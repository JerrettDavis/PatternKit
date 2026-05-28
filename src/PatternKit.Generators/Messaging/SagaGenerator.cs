using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class SagaGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKSG001",
        "Saga type must be partial",
        "Type '{0}' is marked with [GenerateSaga] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingSteps = new(
        "PKSG002",
        "Saga has no steps",
        "Type '{0}' is marked with [GenerateSaga] but does not declare any [SagaStep] methods",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidStep = new(
        "PKSG003",
        "Saga step signature is invalid",
        "Saga step '{0}' must be static and return TState or ValueTask<TState> with the required state/message/context parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidCompletion = new(
        "PKSG004",
        "Saga completion signature is invalid",
        "Saga completion method '{0}' must be static bool with one TState parameter",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateSagaAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateSagaAttribute");
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

        var stateType = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (stateType is null)
            return;

        var hasStepAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.SagaStepAttribute"));
        var steps = GetSteps(type, stateType, context);
        if (steps.Length == 0)
        {
            if (!hasStepAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingSteps, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var completion = GetCompletion(type, stateType, context);
        var syncSteps = steps.Where(static step => !step.IsAsync).OrderBy(static step => step.Order).ThenBy(static step => step.MethodName).ToArray();
        var asyncSteps = steps.Where(static step => step.IsAsync).OrderBy(static step => step.Order).ThenBy(static step => step.MethodName).ToArray();
        var config = new SagaConfig(
            GetNamedString(attribute, "FactoryName") ?? "Create",
            GetNamedString(attribute, "AsyncFactoryName") ?? "CreateAsync");

        context.AddSource($"{type.Name}.Saga.g.cs", SourceText.From(GenerateSource(type, stateType, syncSteps, asyncSteps, completion, config), Encoding.UTF8));
    }

    private static ImmutableArray<SagaStep> GetSteps(
        INamedTypeSymbol type,
        INamedTypeSymbol stateType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<SagaStep>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.SagaStepAttribute");
            if (attr is null)
                continue;

            if (!TryGetStep(method, stateType, attr, out var step))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidStep, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(step);
        }

        return builder.ToImmutable();
    }

    private static string? GetCompletion(
        INamedTypeSymbol type,
        INamedTypeSymbol stateType,
        SourceProductionContext context)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (!method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.SagaCompleteWhenAttribute"))
                continue;

            if (method.IsStatic &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                method.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType))
                return method.Name;

            context.ReportDiagnostic(Diagnostic.Create(InvalidCompletion, method.Locations.FirstOrDefault(), method.Name));
        }

        return null;
    }

    private static bool TryGetStep(
        IMethodSymbol method,
        INamedTypeSymbol stateType,
        AttributeData attribute,
        out SagaStep step)
    {
        step = default;
        if (!method.IsStatic || attribute.ConstructorArguments.Length != 2)
            return false;

        var messageType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        var order = attribute.ConstructorArguments[1].Value as int? ?? 0;
        if (messageType is null)
            return false;

        var syncReturn = SymbolEqualityComparer.Default.Equals(method.ReturnType, stateType);
        var asyncReturn = IsValueTaskOfState(method.ReturnType, stateType);
        if (!syncReturn && !asyncReturn)
            return false;

        if (syncReturn && method.Parameters.Length == 3 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType) &&
            IsMessageOf(method.Parameters[1].Type, messageType) &&
            method.Parameters[2].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext")
        {
            step = new SagaStep(messageType, order, method.Name, isAsync: false);
            return true;
        }

        if (asyncReturn && method.Parameters.Length == 4 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType) &&
            IsMessageOf(method.Parameters[1].Type, messageType) &&
            method.Parameters[2].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
            method.Parameters[3].Type.ToDisplayString() == "System.Threading.CancellationToken")
        {
            step = new SagaStep(messageType, order, method.Name, isAsync: true);
            return true;
        }

        return false;
    }

    private static bool IsMessageOf(ITypeSymbol type, INamedTypeSymbol messageType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], messageType);

    private static bool IsValueTaskOfState(ITypeSymbol type, INamedTypeSymbol stateType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>" &&
           named.TypeArguments.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], stateType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol stateType,
        IReadOnlyList<SagaStep> syncSteps,
        IReadOnlyList<SagaStep> asyncSteps,
        string? completionMethod,
        SagaConfig config)
    {
        var stateName = stateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            sb.Append("namespace ").Append(type.ContainingNamespace.ToDisplayString()).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("partial ").Append(GetKind(type)).Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        if (syncSteps.Count > 0)
        {
            sb.Append("    public static global::PatternKit.Messaging.Sagas.Saga<").Append(stateName).Append("> ")
                .Append(config.FactoryName).AppendLine("()");
            sb.AppendLine("        => global::PatternKit.Messaging.Sagas.Saga<" + stateName + ">.Create()");
            foreach (var step in syncSteps)
                sb.Append("            .On<").Append(step.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">().Then(").Append(step.MethodName).AppendLine(")");
            if (completionMethod is not null)
                sb.Append("            .CompleteWhen(").Append(completionMethod).AppendLine(")");
            sb.AppendLine("            .Build();");
            sb.AppendLine();
        }

        if (asyncSteps.Count > 0)
        {
            sb.Append("    public static global::PatternKit.Messaging.Sagas.AsyncSaga<").Append(stateName).Append("> ")
                .Append(config.AsyncFactoryName).AppendLine("()");
            sb.AppendLine("        => global::PatternKit.Messaging.Sagas.AsyncSaga<" + stateName + ">.Create()");
            foreach (var step in asyncSteps)
                sb.Append("            .On<").Append(step.MessageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">().Then(").Append(step.MethodName).AppendLine(")");
            if (completionMethod is not null)
                sb.Append("            .CompleteWhen(").Append(completionMethod).AppendLine(")");
            sb.AppendLine("            .Build();");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string? GetNamedString(AttributeData attribute, string name)
    {
        foreach (var argument in attribute.NamedArguments)
            if (argument.Key == name)
                return argument.Value.Value as string;

        return null;
    }

    private readonly struct SagaStep
    {
        public SagaStep(INamedTypeSymbol messageType, int order, string methodName, bool isAsync)
            => (MessageType, Order, MethodName, IsAsync) = (messageType, order, methodName, isAsync);

        public INamedTypeSymbol MessageType { get; }

        public int Order { get; }

        public string MethodName { get; }

        public bool IsAsync { get; }
    }

    private readonly struct SagaConfig
    {
        public SagaConfig(string factoryName, string asyncFactoryName)
            => (FactoryName, AsyncFactoryName) = (factoryName, asyncFactoryName);

        public string FactoryName { get; }

        public string AsyncFactoryName { get; }
    }
}
