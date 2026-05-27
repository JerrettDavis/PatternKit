using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class ContentEnricherGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.Messaging.GenerateContentEnricherAttribute";
    private const string StepAttributeName = "PatternKit.Generators.Messaging.ContentEnrichmentStepAttribute";
    private const string MessageContextName = "PatternKit.Messaging.MessageContext";
    private const string CancellationTokenName = "System.Threading.CancellationToken";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKMCE001",
        "Content enricher host must be partial",
        "Type '{0}' is marked with [GenerateContentEnricher] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingSteps = new(
        "PKMCE002",
        "Content enricher has no steps",
        "Content enricher '{0}' must declare at least one [ContentEnrichmentStep] method",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidStep = new(
        "PKMCE003",
        "Content enrichment step signature is invalid",
        "Content enrichment step '{0}' must be static, return ValueTask<TPayload>, and accept TPayload, MessageContext, CancellationToken",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKMCE004",
        "Content enricher configuration is invalid",
        "Content enricher '{0}' has invalid configuration: {1}",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == GenerateAttributeName);
            if (attr is not null)
                Generate(spc, candidate.Type, candidate.Node, attr);
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol type, TypeDeclarationSyntax node, AttributeData attribute)
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

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var enricherName = GetNamedString(attribute, "EnricherName") ?? "content-enricher";
        var defaultPolicy = GetNamedPolicy(attribute, "DefaultPolicy") ?? "Throw";
        if (string.IsNullOrWhiteSpace(factoryName) || string.IsNullOrWhiteSpace(enricherName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), type.Name, "factory and enricher names are required"));
            return;
        }

        var steps = type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => (Method: method, Attribute: method.GetAttributes().FirstOrDefault(static attr => attr.AttributeClass?.ToDisplayString() == StepAttributeName)))
            .Where(static item => item.Attribute is not null)
            .Select(static item => Step.From(item.Method, item.Attribute!))
            .OrderBy(static step => step.Order)
            .ThenBy(static step => step.Method.Name)
            .ToArray();

        if (steps.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingSteps, node.Identifier.GetLocation(), type.Name));
            return;
        }

        foreach (var step in steps)
        {
            if (!IsStep(step.Method, payloadType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidStep, step.Method.Locations.FirstOrDefault(), step.Method.Name));
                return;
            }

            if (step.Policy == "UseDefault")
            {
                if (string.IsNullOrWhiteSpace(step.DefaultFactoryName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, step.Method.Locations.FirstOrDefault(), type.Name, $"step '{step.Name}' uses UseDefault without DefaultFactoryName"));
                    return;
                }

                var factory = type.GetMembers(step.DefaultFactoryName).OfType<IMethodSymbol>().FirstOrDefault();
                if (factory is null || !IsDefaultFactory(factory, payloadType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, step.Method.Locations.FirstOrDefault(), type.Name, $"default factory '{step.DefaultFactoryName}' must be static and accept/return the payload type"));
                    return;
                }
            }
        }

        context.AddSource($"{type.Name}.ContentEnricher.g.cs", SourceText.From(
            GenerateSource(type, payloadType, factoryName, enricherName, defaultPolicy, steps),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        string factoryName,
        string enricherName,
        string defaultPolicy,
        IReadOnlyList<Step> steps)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var payloadName = payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public static global::PatternKit.Messaging.Transformation.AsyncContentEnricher<")
            .Append(payloadName).Append("> ").Append(factoryName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Messaging.Transformation.AsyncContentEnricher<")
            .Append(payloadName).Append(">.Create(\"").Append(Escape(enricherName)).AppendLine("\")");
        sb.Append("            .WithDefaultPolicy(global::PatternKit.Messaging.Transformation.EnrichmentErrorPolicy.")
            .Append(defaultPolicy).AppendLine(");");

        foreach (var step in steps)
        {
            sb.Append("        builder.Enrich(\"").Append(Escape(step.Name)).Append("\", static (payload, context, cancellationToken) => ")
                .Append(step.Method.Name).Append("(payload, context, cancellationToken)");
            if (step.Policy != defaultPolicy || step.Policy == "UseDefault")
            {
                sb.Append(", global::PatternKit.Messaging.Transformation.EnrichmentErrorPolicy.").Append(step.Policy);
                if (step.Policy == "UseDefault")
                    sb.Append(", static payload => ").Append(step.DefaultFactoryName).Append("(payload)");
            }

            sb.AppendLine(");");
        }

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsStep(IMethodSymbol method, ITypeSymbol payloadType)
        => method.IsStatic
        && !method.IsGenericMethod
        && IsValueTaskOfPayload(method.ReturnType, payloadType)
        && method.Parameters.Length == 3
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, payloadType)
        && method.Parameters[1].Type.ToDisplayString() == MessageContextName
        && method.Parameters[2].Type.ToDisplayString() == CancellationTokenName;

    private static bool IsDefaultFactory(IMethodSymbol method, ITypeSymbol payloadType)
        => method.IsStatic
        && !method.IsGenericMethod
        && SymbolEqualityComparer.Default.Equals(method.ReturnType, payloadType)
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, payloadType);

    private static bool IsValueTaskOfPayload(ITypeSymbol type, ITypeSymbol payloadType)
        => type is INamedTypeSymbol named
        && named.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>"
        && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string? GetNamedPolicy(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value;
        return value switch
        {
            0 => "Throw",
            1 => "Skip",
            2 => "UseDefault",
            _ => value?.ToString()
        };
    }

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string GetAccessibility(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal"
        };

    private readonly record struct Step(IMethodSymbol Method, string Name, int Order, string Policy, string DefaultFactoryName)
    {
        internal static Step From(IMethodSymbol method, AttributeData attribute)
            => new(
                method,
                attribute.ConstructorArguments[0].Value as string ?? method.Name,
                GetNamedInt(attribute, "Order") ?? 0,
                GetNamedPolicy(attribute, "Policy") ?? "Throw",
                GetNamedString(attribute, "DefaultFactoryName") ?? string.Empty);
    }
}
