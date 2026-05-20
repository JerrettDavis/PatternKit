using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class SplitterAggregatorGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKSA001",
        "Splitter or aggregator type must be partial",
        "Type '{0}' is marked for generated splitter or aggregator factories but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingSplitterProjection = new(
        "PKSA002",
        "Splitter projection is missing",
        "Type '{0}' is marked with [GenerateSplitter] but does not declare exactly one [SplitterProjection] method",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidSplitterProjection = new(
        "PKSA003",
        "Splitter projection signature is invalid",
        "Splitter projection '{0}' must be static and return IEnumerable<TItem> with Message<TPayload> and MessageContext parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingAggregatorParts = new(
        "PKSA004",
        "Aggregator methods are missing",
        "Type '{0}' is marked with [GenerateAggregator] but must declare exactly one correlation, completion, and projection method",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidAggregatorMethod = new(
        "PKSA005",
        "Aggregator method signature is invalid",
        "Aggregator method '{0}' has an invalid signature for the generated key, item, and result types",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidDuplicatePolicy = new(
        "PKSA006",
        "Aggregator duplicate policy is invalid",
        "Generated aggregator duplicate policy '{0}' is invalid. Supported values are Ignore, Include, and Replace.",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var splitters = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateSplitterAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(splitters, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateSplitterAttribute");
            if (attr is not null)
                GenerateSplitter(spc, candidate.Type, candidate.Node, attr);
        });

        var aggregators = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateAggregatorAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(aggregators, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateAggregatorAttribute");
            if (attr is not null)
                GenerateAggregator(spc, candidate.Type, candidate.Node, attr);
        });
    }

    private static void GenerateSplitter(
        SourceProductionContext context,
        INamedTypeSymbol type,
        TypeDeclarationSyntax node,
        AttributeData attribute)
    {
        if (!IsPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var payloadType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        var itemType = attribute.ConstructorArguments.Length >= 2
            ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol
            : null;
        if (payloadType is null || itemType is null)
            return;

        var projections = GetMarkedMethods(type, "PatternKit.Generators.Messaging.SplitterProjectionAttribute");
        if (projections.Count != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingSplitterProjection, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var projection = projections[0];
        if (!IsSplitterProjection(projection, payloadType, itemType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidSplitterProjection, projection.Locations.FirstOrDefault(), projection.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        context.AddSource($"{type.Name}.Splitter.g.cs", SourceText.From(
            GenerateSplitterSource(type, payloadType, itemType, projection.Name, factoryName),
            Encoding.UTF8));
    }

    private static void GenerateAggregator(
        SourceProductionContext context,
        INamedTypeSymbol type,
        TypeDeclarationSyntax node,
        AttributeData attribute)
    {
        if (!IsPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var keyType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        var itemType = attribute.ConstructorArguments.Length >= 2
            ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol
            : null;
        var resultType = attribute.ConstructorArguments.Length >= 3
            ? attribute.ConstructorArguments[2].Value as INamedTypeSymbol
            : null;
        if (keyType is null || itemType is null || resultType is null)
            return;

        var correlationMethods = GetMarkedMethods(type, "PatternKit.Generators.Messaging.AggregatorCorrelationAttribute");
        var completionMethods = GetMarkedMethods(type, "PatternKit.Generators.Messaging.AggregatorCompletionAttribute");
        var projectionMethods = GetMarkedMethods(type, "PatternKit.Generators.Messaging.AggregatorProjectionAttribute");
        if (correlationMethods.Count != 1 || completionMethods.Count != 1 || projectionMethods.Count != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingAggregatorParts, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var correlation = correlationMethods[0];
        var completion = completionMethods[0];
        var projection = projectionMethods[0];
        if (!IsAggregatorCorrelation(correlation, keyType, itemType))
            context.ReportDiagnostic(Diagnostic.Create(InvalidAggregatorMethod, correlation.Locations.FirstOrDefault(), correlation.Name));
        if (!IsAggregatorCompletion(completion, keyType, itemType))
            context.ReportDiagnostic(Diagnostic.Create(InvalidAggregatorMethod, completion.Locations.FirstOrDefault(), completion.Name));
        if (!IsAggregatorProjection(projection, keyType, itemType, resultType))
            context.ReportDiagnostic(Diagnostic.Create(InvalidAggregatorMethod, projection.Locations.FirstOrDefault(), projection.Name));
        if (!IsAggregatorCorrelation(correlation, keyType, itemType) ||
            !IsAggregatorCompletion(completion, keyType, itemType) ||
            !IsAggregatorProjection(projection, keyType, itemType, resultType))
        {
            return;
        }

        var duplicatePolicy = GetNamedString(attribute, "DuplicatePolicy") ?? "Ignore";
        if (!TryNormalizeDuplicatePolicy(duplicatePolicy, out var normalizedPolicy))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidDuplicatePolicy, node.Identifier.GetLocation(), duplicatePolicy));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        context.AddSource($"{type.Name}.Aggregator.g.cs", SourceText.From(
            GenerateAggregatorSource(type, keyType, itemType, resultType, correlation.Name, completion.Name, projection.Name, normalizedPolicy, factoryName),
            Encoding.UTF8));
    }

    private static List<IMethodSymbol> GetMarkedMethods(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToList();

    private static bool IsSplitterProjection(IMethodSymbol method, INamedTypeSymbol payloadType, INamedTypeSymbol itemType)
        => method.IsStatic &&
           method.Parameters.Length == 2 &&
           IsMessageOf(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           IsEnumerableOf(method.ReturnType, itemType);

    private static bool IsAggregatorCorrelation(IMethodSymbol method, INamedTypeSymbol keyType, INamedTypeSymbol itemType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, keyType) &&
           method.Parameters.Length == 2 &&
           IsMessageOf(method.Parameters[0].Type, itemType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsAggregatorCompletion(IMethodSymbol method, INamedTypeSymbol keyType, INamedTypeSymbol itemType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 3 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, keyType) &&
           IsReadOnlyListOfMessage(method.Parameters[1].Type, itemType) &&
           method.Parameters[2].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsAggregatorProjection(IMethodSymbol method, INamedTypeSymbol keyType, INamedTypeSymbol itemType, INamedTypeSymbol resultType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, resultType) &&
           method.Parameters.Length == 3 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, keyType) &&
           IsReadOnlyListOfMessage(method.Parameters[1].Type, itemType) &&
           method.Parameters[2].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsMessageOf(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool IsEnumerableOf(ITypeSymbol type, INamedTypeSymbol itemType)
    {
        if (type is INamedTypeSymbol named &&
            named.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>" &&
            SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], itemType))
        {
            return true;
        }

        return type.AllInterfaces.Any(iface =>
            iface.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>" &&
            SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], itemType));
    }

    private static bool IsReadOnlyListOfMessage(ITypeSymbol type, INamedTypeSymbol itemType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IReadOnlyList<T>" &&
           named.TypeArguments.Length == 1 &&
           IsMessageOf(named.TypeArguments[0], itemType);

    private static string GenerateSplitterSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        INamedTypeSymbol itemType,
        string projectionMethodName,
        string factoryName)
    {
        var payload = payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var item = itemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sb = CreatePreamble(type);
        sb.Append("    public static global::PatternKit.Messaging.Routing.Splitter<")
            .Append(payload)
            .Append(", ")
            .Append(item)
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Routing.Splitter<")
            .Append(payload)
            .Append(", ")
            .Append(item)
            .AppendLine(">.Create()");
        sb.Append("            .Use(").Append(projectionMethodName).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateAggregatorSource(
        INamedTypeSymbol type,
        INamedTypeSymbol keyType,
        INamedTypeSymbol itemType,
        INamedTypeSymbol resultType,
        string correlationMethodName,
        string completionMethodName,
        string projectionMethodName,
        string duplicatePolicy,
        string factoryName)
    {
        var key = keyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var item = itemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var result = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sb = CreatePreamble(type);
        sb.Append("    public static global::PatternKit.Messaging.Routing.Aggregator<")
            .Append(key)
            .Append(", ")
            .Append(item)
            .Append(", ")
            .Append(result)
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Routing.Aggregator<")
            .Append(key)
            .Append(", ")
            .Append(item)
            .Append(", ")
            .Append(result)
            .AppendLine(">.Create()");
        sb.Append("            .KeyBy(").Append(correlationMethodName).AppendLine(")");
        sb.Append("            .CompleteWhen(").Append(completionMethodName).AppendLine(")");
        sb.Append("            .Project(").Append(projectionMethodName).AppendLine(")");
        sb.Append("            .Duplicates(global::PatternKit.Messaging.Routing.DuplicateMessagePolicy.").Append(duplicatePolicy).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static StringBuilder CreatePreamble(INamedTypeSymbol type)
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
        return sb;
    }

    private static bool IsPartial(TypeDeclarationSyntax node)
        => node.Modifiers.Any(static modifier => modifier.Text == "partial");

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static bool TryNormalizeDuplicatePolicy(string value, out string normalized)
    {
        normalized = value;
        if (string.Equals(value, "Ignore", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Ignore";
        else if (string.Equals(value, "Include", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Include";
        else if (string.Equals(value, "Replace", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Replace";
        else
            return false;

        return true;
    }
}
