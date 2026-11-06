using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.CacheAside;

[Generator]
public sealed class CacheAsidePolicyGenerator : IIncrementalGenerator
{
    private const string GenerateCacheAsidePolicyAttributeName = "PatternKit.Generators.CacheAside.GenerateCacheAsidePolicyAttribute";
    private const string CacheAsidePredicateAttributeName = "PatternKit.Generators.CacheAside.CacheAsidePredicateAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCA001",
        "Cache-aside policy host must be partial",
        "Type '{0}' is marked with [GenerateCacheAsidePolicy] but is not declared as partial",
        "PatternKit.Generators.CacheAside",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKCA002",
        "Cache-aside policy configuration is invalid",
        "Cache-aside policy '{0}' must have TimeToLiveMilliseconds >= 0",
        "PatternKit.Generators.CacheAside",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidPredicate = new(
        "PKCA003",
        "Cache-aside predicate signature is invalid",
        "Cache-aside predicate method '{0}' must be static and return bool with one result parameter",
        "PatternKit.Generators.CacheAside",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MultiplePredicates = new(
        "PKCA004",
        "Cache-aside predicate is duplicated",
        "Cache-aside policy '{0}' has multiple cache predicates",
        "PatternKit.Generators.CacheAside",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateCacheAsidePolicyAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == GenerateCacheAsidePolicyAttributeName);
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

        var resultType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (resultType is null)
            return;

        var timeToLiveMilliseconds = GetNamedInt(attribute, "TimeToLiveMilliseconds") ?? 0;
        if (timeToLiveMilliseconds < 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var predicates = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => HasAttribute(method, CacheAsidePredicateAttributeName))
            .ToArray();
        if (predicates.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MultiplePredicates, predicates[1].Locations.FirstOrDefault(), type.Name));
            return;
        }

        var predicate = predicates.FirstOrDefault();
        if (predicate is not null && !IsCachePredicate(predicate, resultType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidPredicate, predicate.Locations.FirstOrDefault(), predicate.Name));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        var policyName = GetNamedString(attribute, "PolicyName") ?? "cache-aside";
        context.AddSource($"{type.Name}.CacheAsidePolicy.g.cs", SourceText.From(
            GenerateSource(type, resultType, factoryMethodName, policyName, timeToLiveMilliseconds, predicate),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol resultType,
        string factoryMethodName,
        string policyName,
        int timeToLiveMilliseconds,
        IMethodSymbol? predicate)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var resultTypeName = resultType.ToDisplayString(TypeFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        var indent = "";
        foreach (var containingType in GetContainingTypes(type))
        {
            AppendTypeDeclaration(sb, containingType, indent);
            sb.Append(indent).AppendLine("{");
            indent += "    ";
        }

        AppendTypeDeclaration(sb, type, indent);
        sb.AppendLine("{");
        sb.Append(indent).Append("    public static global::PatternKit.Cloud.CacheAside.CacheAsidePolicy<").Append(resultTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.Append(indent).AppendLine("    {");
        sb.Append(indent).Append("        var builder = global::PatternKit.Cloud.CacheAside.CacheAsidePolicy<").Append(resultTypeName).Append(">.Create(\"").Append(Escape(policyName)).AppendLine("\");");

        if (timeToLiveMilliseconds > 0)
            sb.Append(indent).Append("        builder.WithTimeToLive(global::System.TimeSpan.FromMilliseconds(").Append(timeToLiveMilliseconds).AppendLine("));");
        else
            sb.Append(indent).AppendLine("        builder.WithoutExpiration();");

        if (predicate is not null)
            sb.Append(indent).Append("        builder.CacheWhen(static value => ").Append(predicate.Name).AppendLine("(value));");

        sb.Append(indent).AppendLine("        return builder.Build();");
        sb.Append(indent).AppendLine("    }");
        sb.Append(indent).AppendLine("}");

        while (indent.Length > 0)
        {
            indent = indent.Substring(4);
            sb.Append(indent).AppendLine("}");
        }

        return sb.ToString();
    }

    private static IReadOnlyList<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol type)
    {
        var stack = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
            stack.Push(current);
        return stack.ToArray();
    }

    private static void AppendTypeDeclaration(StringBuilder sb, INamedTypeSymbol type, string indent)
    {
        sb.Append(indent).Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ')
            .Append(type.Name).Append(GetTypeParameterList(type)).Append(GetConstraintClauses(type)).AppendLine();
    }

    private static string GetTypeParameterList(INamedTypeSymbol type)
        => type.TypeParameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", type.TypeParameters.Select(static parameter => parameter.Name)) + ">";

    private static string GetConstraintClauses(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0)
            return string.Empty;

        var clauses = new List<string>();
        foreach (var parameter in type.TypeParameters)
        {
            var constraints = new List<string>();
            if (parameter.HasReferenceTypeConstraint)
                constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class");
            if (parameter.HasNotNullConstraint)
                constraints.Add("notnull");
            if (parameter.HasUnmanagedTypeConstraint)
                constraints.Add("unmanaged");
            else if (parameter.HasValueTypeConstraint)
                constraints.Add("struct");

            constraints.AddRange(parameter.ConstraintTypes.Select(static constraint => constraint.ToDisplayString(TypeFormat)));
            if (parameter.HasConstructorConstraint)
                constraints.Add("new()");
            if (constraints.Count > 0)
                clauses.Add($" where {parameter.Name} : {string.Join(", ", constraints)}");
        }

        return string.Concat(clauses);
    }

    private static bool IsCachePredicate(IMethodSymbol method, ITypeSymbol resultType)
        => method.IsStatic
        && method.ReturnType.SpecialType == SpecialType.System_Boolean
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, resultType);

    private static bool HasAttribute(IMethodSymbol method, string metadataName)
        => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == metadataName);

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

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;
}
