using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.ContextMaps;

[Generator]
public sealed class ContextMapDescriptorGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.ContextMaps.GenerateContextMapDescriptorAttribute";
    private const string RelationshipAttributeName = "PatternKit.Generators.ContextMaps.ContextMapRelationshipAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCMAP001",
        "Context map descriptor host must be partial",
        "Type '{0}' is marked with [GenerateContextMapDescriptor] but is not declared as partial",
        "PatternKit.Generators.ContextMaps",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingRelationships = new(
        "PKCMAP002",
        "Context map descriptor has no relationships",
        "Type '{0}' is marked with [GenerateContextMapDescriptor] but does not declare any context map relationships",
        "PatternKit.Generators.ContextMaps",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRelationship = new(
        "PKCMAP003",
        "Context map relationship is duplicated",
        "Context map relationship '{0}' is registered more than once",
        "PatternKit.Generators.ContextMaps",
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

        var name = attribute.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(name))
            return;

        var relationships = GetRelationships(type);
        if (relationships.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingRelationships, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (TryFindDuplicate(relationships, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRelationship, duplicate.Location, duplicate.Key));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        context.AddSource($"{type.Name}.ContextMapDescriptor.g.cs", SourceText.From(
            GenerateSource(type, name!, relationships, factoryMethodName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Relationship> GetRelationships(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<Relationship>();
        foreach (var attr in type.GetAttributes().Where(static attr => attr.AttributeClass?.ToDisplayString() == RelationshipAttributeName))
        {
            var upstream = attr.ConstructorArguments[0].Value as string;
            var downstream = attr.ConstructorArguments[1].Value as string;
            var kind = attr.ConstructorArguments[2].Value;
            var contract = attr.ConstructorArguments[3].Value as string;
            if (!string.IsNullOrWhiteSpace(upstream)
                && !string.IsNullOrWhiteSpace(downstream)
                && kind is int kindValue
                && !string.IsNullOrWhiteSpace(contract))
            {
                builder.Add(new Relationship(
                    upstream!,
                    downstream!,
                    kindValue,
                    contract!,
                    $"{upstream}->{downstream}:{contract}",
                    attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
            }
        }

        return builder.ToImmutable();
    }

    private static bool TryFindDuplicate(IReadOnlyList<Relationship> relationships, out Relationship duplicate)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var relationship in relationships)
        {
            if (!seen.Add(relationship.Key))
            {
                duplicate = relationship;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        string mapName,
        IReadOnlyList<Relationship> relationships,
        string factoryMethodName)
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
        sb.Append("    public static global::PatternKit.Application.ContextMaps.ContextMapDescriptor ")
            .Append(factoryMethodName)
            .AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Application.ContextMaps.ContextMapDescriptor.Create(\"")
            .Append(Escape(mapName))
            .AppendLine("\");");

        foreach (var relationship in relationships.OrderBy(static relationship => relationship.Key, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.AddRelationship(\"")
                .Append(Escape(relationship.UpstreamContext))
                .Append("\", \"")
                .Append(Escape(relationship.DownstreamContext))
                .Append("\", global::PatternKit.Application.ContextMaps.ContextRelationshipKind.")
                .Append(GetRelationshipKindName(relationship.Kind))
                .Append(", \"")
                .Append(Escape(relationship.ContractName))
                .AppendLine("\");");
        }

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string GetRelationshipKindName(int value)
        => value switch
        {
            0 => "Partnership",
            1 => "SharedKernel",
            2 => "CustomerSupplier",
            3 => "Conformist",
            4 => "AntiCorruptionLayer",
            5 => "OpenHostService",
            6 => "PublishedLanguage",
            7 => "SeparateWays",
            _ => "SeparateWays"
        };

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private readonly record struct Relationship(
        string UpstreamContext,
        string DownstreamContext,
        int Kind,
        string ContractName,
        string Key,
        Location? Location);
}
