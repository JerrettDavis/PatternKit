using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Aggregates;

[Generator]
public sealed class AggregateCommandHandlerGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.Aggregates.GenerateAggregateCommandHandlerAttribute";
    private const string DecisionAttributeName = "PatternKit.Generators.Aggregates.AggregateDecisionAttribute";
    private const string ApplierAttributeName = "PatternKit.Generators.Aggregates.AggregateEventApplierAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKAGG001",
        "Aggregate handler host must be partial",
        "Type '{0}' is marked with [GenerateAggregateCommandHandler] but is not declared as partial",
        "PatternKit.Generators.Aggregates",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingDecision = new(
        "PKAGG002",
        "Aggregate handler must declare one decision method",
        "Type '{0}' must declare exactly one [AggregateDecision] method",
        "PatternKit.Generators.Aggregates",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingApplier = new(
        "PKAGG003",
        "Aggregate handler must declare one event applier",
        "Type '{0}' must declare exactly one [AggregateEventApplier] method",
        "PatternKit.Generators.Aggregates",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidDecision = new(
        "PKAGG004",
        "Aggregate decision signature is invalid",
        "Decision method '{0}' must be static, return IEnumerable<TEvent>, and accept TAggregate and TCommand parameters",
        "PatternKit.Generators.Aggregates",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidApplier = new(
        "PKAGG005",
        "Aggregate event applier signature is invalid",
        "Event applier method '{0}' must be static, return void, and accept TAggregate and TEvent parameters",
        "PatternKit.Generators.Aggregates",
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
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == GenerateAttributeName);
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

        var aggregateType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        var commandType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
        var eventType = attribute.ConstructorArguments[2].Value as INamedTypeSymbol;
        if (aggregateType is null || commandType is null || eventType is null)
            return;

        var decisions = GetMethods(type, DecisionAttributeName);
        var appliers = GetMethods(type, ApplierAttributeName);
        if (decisions.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingDecision, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (appliers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingApplier, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var decision = decisions[0];
        if (!IsDecision(decision, aggregateType, commandType, eventType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidDecision, decision.Locations.FirstOrDefault(), decision.Name));
            return;
        }

        var applier = appliers[0];
        if (!IsApplier(applier, aggregateType, eventType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidApplier, applier.Locations.FirstOrDefault(), applier.Name));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        var handlerName = GetNamedString(attribute, "HandlerName") ?? "generated-aggregate-handler";
        context.AddSource($"{type.Name}.AggregateCommandHandler.g.cs", SourceText.From(
            GenerateSource(type, aggregateType, commandType, eventType, decision, applier, factoryMethodName, handlerName),
            Encoding.UTF8));
    }

    private static ImmutableArray<IMethodSymbol> GetMethods(INamedTypeSymbol type, string attributeName)
        => type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToImmutableArray();

    private static bool IsDecision(IMethodSymbol method, INamedTypeSymbol aggregateType, INamedTypeSymbol commandType, INamedTypeSymbol eventType)
    {
        if (!method.IsStatic || method.Parameters.Length != 2)
            return false;

        if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, aggregateType)
            || !SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, commandType))
            return false;

        return IsEnumerableOf(method.ReturnType, eventType);
    }

    private static bool IsApplier(IMethodSymbol method, INamedTypeSymbol aggregateType, INamedTypeSymbol eventType)
        => method.IsStatic
            && method.ReturnsVoid
            && method.Parameters.Length == 2
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, aggregateType)
            && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, eventType);

    private static bool IsEnumerableOf(ITypeSymbol type, INamedTypeSymbol eventType)
    {
        if (type is IArrayTypeSymbol array)
            return SymbolEqualityComparer.Default.Equals(array.ElementType, eventType);

        if (type is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], eventType))
            return true;

        return type.AllInterfaces.Any(candidate =>
            candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], eventType));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol aggregateType,
        INamedTypeSymbol commandType,
        INamedTypeSymbol eventType,
        IMethodSymbol decision,
        IMethodSymbol applier,
        string factoryMethodName,
        string handlerName)
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

        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public static global::PatternKit.Application.Aggregates.AggregateCommandHandler<")
            .Append(aggregateType.ToDisplayString(TypeFormat)).Append(", ")
            .Append(commandType.ToDisplayString(TypeFormat)).Append(", ")
            .Append(eventType.ToDisplayString(TypeFormat)).Append("> ")
            .Append(factoryMethodName).AppendLine("()");
        sb.Append("        => global::PatternKit.Application.Aggregates.AggregateCommandHandler<")
            .Append(aggregateType.ToDisplayString(TypeFormat)).Append(", ")
            .Append(commandType.ToDisplayString(TypeFormat)).Append(", ")
            .Append(eventType.ToDisplayString(TypeFormat)).Append(">.Create(\"")
            .Append(Escape(handlerName)).Append("\", ")
            .Append(decision.Name).Append(", ")
            .Append(applier.Name).AppendLine(");");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
