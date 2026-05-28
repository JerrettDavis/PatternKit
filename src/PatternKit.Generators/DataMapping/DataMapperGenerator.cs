using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.DataMapping;

[Generator]
public sealed class DataMapperGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.DataMapping.GenerateDataMapperAttribute";
    private const string ToDataAttributeName = "PatternKit.Generators.DataMapping.DataMapperToDataAttribute";
    private const string ToDomainAttributeName = "PatternKit.Generators.DataMapping.DataMapperToDomainAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKMAP001",
        "Data Mapper host must be partial",
        "Type '{0}' is marked with [GenerateDataMapper] but is not declared as partial",
        "PatternKit.Generators.DataMapping",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingProjection = new(
        "PKMAP002",
        "Data Mapper projections are missing",
        "Data Mapper '{0}' must declare exactly one [DataMapperToData] method and exactly one [DataMapperToDomain] method",
        "PatternKit.Generators.DataMapping",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidProjection = new(
        "PKMAP003",
        "Data Mapper projection signature is invalid",
        "Data Mapper projection '{0}' must be static and return the target type from one source parameter",
        "PatternKit.Generators.DataMapping",
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

        var domainType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var dataType = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (domainType is null || dataType is null)
            return;

        var toData = FindProjection(type, ToDataAttributeName);
        var toDomain = FindProjection(type, ToDomainAttributeName);
        if (toData.Length != 1 || toDomain.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingProjection, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (!IsProjection(toData[0], domainType, dataType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidProjection, toData[0].Locations.FirstOrDefault(), toData[0].Name));
            return;
        }

        if (!IsProjection(toDomain[0], dataType, domainType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidProjection, toDomain[0].Locations.FirstOrDefault(), toDomain[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.DataMapper.g.cs", SourceText.From(
            GenerateSource(type, domainType, dataType, toData[0].Name, toDomain[0].Name, GetNamedString(attribute, "FactoryName") ?? "Create"),
            Encoding.UTF8));
    }

    private static IMethodSymbol[] FindProjection(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol domainType,
        INamedTypeSymbol dataType,
        string toDataName,
        string toDomainName,
        string factoryName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var domainName = domainType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var dataName = dataType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        sb.Append("    public static global::PatternKit.Application.DataMapping.DataMapper<")
            .Append(domainName).Append(", ").Append(dataName).Append("> ").Append(factoryName).AppendLine("()");
        sb.Append("        => global::PatternKit.Application.DataMapping.DataMapper<")
            .Append(domainName).Append(", ").Append(dataName).AppendLine(">.Create()");
        sb.Append("            .MapToData(").Append(toDataName).AppendLine(")");
        sb.Append("            .MapToDomain(").Append(toDomainName).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsProjection(IMethodSymbol method, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, sourceType)
        && SymbolEqualityComparer.Default.Equals(method.ReturnType, targetType);

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

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
}
