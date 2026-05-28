using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.GatewayAggregation;

[Generator]
public sealed class GatewayAggregationGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.GatewayAggregation.GenerateGatewayAggregationAttribute";
    private const string FetchAttributeName = "PatternKit.Generators.GatewayAggregation.GatewayAggregationFetchAttribute";
    private const string ComposerAttributeName = "PatternKit.Generators.GatewayAggregation.GatewayAggregationComposerAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKGA001", "Gateway Aggregation host must be partial",
        "Type '{0}' is marked with [GenerateGatewayAggregation] but is not declared as partial",
        "PatternKit.Generators.GatewayAggregation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKGA002", "Gateway Aggregation members are missing",
        "Gateway Aggregation type '{0}' must declare at least one fetch and exactly one composer",
        "PatternKit.Generators.GatewayAggregation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKGA003", "Gateway Aggregation method signature is invalid",
        "Gateway Aggregation method '{0}' has an invalid static signature for the configured request or response type",
        "PatternKit.Generators.GatewayAggregation", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateFetch = new(
        "PKGA004", "Gateway Aggregation fetch is duplicated",
        "Gateway Aggregation fetch name '{0}' is duplicated",
        "PatternKit.Generators.GatewayAggregation", DiagnosticSeverity.Error, true);

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == AttributeName);
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

        var requestType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var responseType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (requestType is null || responseType is null)
            return;

        var fetches = FetchMembers(type);
        var composers = MembersWith(type, ComposerAttributeName);
        if (fetches.Length == 0 || composers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var duplicate = fetches.GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateFetch, node.Identifier.GetLocation(), duplicate.Key));
            return;
        }

        var invalidFetch = fetches.FirstOrDefault(item => !IsFetch(item.Method, requestType));
        if (invalidFetch is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidFetch.Method.Locations.FirstOrDefault(), invalidFetch.Method.Name));
            return;
        }

        if (!IsComposer(composers[0], requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, composers[0].Locations.FirstOrDefault(), composers[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.GatewayAggregation.g.cs", SourceText.From(GenerateSource(
            type,
            requestType,
            responseType,
            fetches,
            composers[0].Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "GatewayName") ?? "gateway-aggregation"), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static FetchMember[] FetchMembers(INamedTypeSymbol type)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == FetchAttributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(static item => new FetchMember((string)item.Attribute!.ConstructorArguments[0].Value!, item.Method))
            .ToArray();

    private static bool IsFetch(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           method.ReturnsVoid == false &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType);

    private static bool IsComposer(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, responseType) &&
           method.Parameters.Length == 1 &&
           method.Parameters[0].Type is INamedTypeSymbol contextType &&
           contextType.ConstructedFrom.ToDisplayString() == "PatternKit.Cloud.GatewayAggregation.GatewayAggregationContext<TRequest>" &&
           contextType.TypeArguments.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(contextType.TypeArguments[0], requestType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        IReadOnlyList<FetchMember> fetches,
        string composerName,
        string factoryMethodName,
        string gatewayName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var requestTypeName = requestType.ToDisplayString(TypeFormat);
        var responseTypeName = responseType.ToDisplayString(TypeFormat);
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
        sb.Append("    public static global::PatternKit.Cloud.GatewayAggregation.GatewayAggregation<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.Cloud.GatewayAggregation.GatewayAggregation<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append(">.Create(\"").Append(Escape(gatewayName)).AppendLine("\")");
        foreach (var fetch in fetches)
            sb.Append("            .Fetch<").Append(fetch.Method.ReturnType.ToDisplayString(TypeFormat)).Append(">(\"").Append(Escape(fetch.Name)).Append("\", ").Append(fetch.Method.Name).AppendLine(")");
        sb.Append("            .Compose(").Append(composerName).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

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

    private sealed record FetchMember(string Name, IMethodSymbol Method);
}
