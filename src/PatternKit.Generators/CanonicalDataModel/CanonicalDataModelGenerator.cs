using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.CanonicalDataModel;

[Generator]
public sealed class CanonicalDataModelGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.CanonicalDataModel.GenerateCanonicalDataModelAttribute";
    private const string MapperAttributeName = "PatternKit.Generators.CanonicalDataModel.CanonicalDataModelMapperAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCDM001", "Canonical Data Model host must be partial",
        "Type '{0}' is marked with [GenerateCanonicalDataModel] but is not declared as partial",
        "PatternKit.Generators.CanonicalDataModel", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMapper = new(
        "PKCDM002", "Canonical Data Model mapper is missing",
        "Canonical Data Model type '{0}' must declare exactly one [CanonicalDataModelMapper] method",
        "PatternKit.Generators.CanonicalDataModel", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMapper = new(
        "PKCDM003", "Canonical Data Model mapper signature is invalid",
        "Canonical Data Model mapper '{0}' must be static and return TCanonical with one TSource parameter",
        "PatternKit.Generators.CanonicalDataModel", DiagnosticSeverity.Error, true);

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

        var sourceType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var canonicalType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (sourceType is null || canonicalType is null)
            return;

        var mappers = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == MapperAttributeName))
            .ToArray();
        if (mappers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMapper, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (!IsMapper(mappers[0], sourceType, canonicalType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMapper, mappers[0].Locations.FirstOrDefault(), mappers[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.CanonicalDataModel.g.cs", SourceText.From(GenerateSource(
            type,
            sourceType,
            canonicalType,
            mappers[0].Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "ModelName") ?? "canonical-data-model",
            GetNamedString(attribute, "AdapterName") ?? "source-adapter"), Encoding.UTF8));
    }

    private static bool IsMapper(IMethodSymbol method, INamedTypeSymbol sourceType, INamedTypeSymbol canonicalType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, canonicalType) &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, sourceType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol canonicalType,
        string mapperName,
        string factoryMethodName,
        string modelName,
        string adapterName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var sourceTypeName = sourceType.ToDisplayString(TypeFormat);
        var canonicalTypeName = canonicalType.ToDisplayString(TypeFormat);
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
        sb.Append("    public static global::PatternKit.EnterpriseIntegration.CanonicalDataModel.CanonicalDataModel<").Append(canonicalTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.EnterpriseIntegration.CanonicalDataModel.CanonicalDataModel<").Append(canonicalTypeName).Append(">.Create(\"").Append(Escape(modelName)).AppendLine("\")");
        sb.Append("            .From<").Append(sourceTypeName).Append(">(\"").Append(Escape(adapterName)).Append("\", ").Append(mapperName).AppendLine(")");
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
}
