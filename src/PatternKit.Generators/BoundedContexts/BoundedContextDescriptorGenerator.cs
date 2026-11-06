using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.BoundedContexts;

[Generator]
public sealed class BoundedContextDescriptorGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.BoundedContexts.GenerateBoundedContextDescriptorAttribute";
    private const string CapabilityAttributeName = "PatternKit.Generators.BoundedContexts.BoundedContextCapabilityAttribute";
    private const string AdapterAttributeName = "PatternKit.Generators.BoundedContexts.BoundedContextAdapterAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCTX001",
        "Bounded context descriptor host must be partial",
        "Type '{0}' is marked with [GenerateBoundedContextDescriptor] but is not declared as partial",
        "PatternKit.Generators.BoundedContexts",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingCapabilities = new(
        "PKCTX002",
        "Bounded context descriptor has no capabilities",
        "Type '{0}' is marked with [GenerateBoundedContextDescriptor] but does not declare any bounded context capabilities",
        "PatternKit.Generators.BoundedContexts",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateCapability = new(
        "PKCTX003",
        "Bounded context capability is duplicated",
        "Bounded context capability '{0}' is registered more than once",
        "PatternKit.Generators.BoundedContexts",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateAdapter = new(
        "PKCTX004",
        "Bounded context adapter is duplicated",
        "Bounded context adapter '{0}' is registered more than once",
        "PatternKit.Generators.BoundedContexts",
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

        var capabilities = GetCapabilities(type);
        if (capabilities.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingCapabilities, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (TryFindDuplicateCapability(capabilities, out var duplicateCapability))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateCapability, duplicateCapability.Location, duplicateCapability.Name));
            return;
        }

        var adapters = GetAdapters(type);
        if (TryFindDuplicateAdapter(adapters, out var duplicateAdapter))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateAdapter, duplicateAdapter.Location, duplicateAdapter.Key));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        context.AddSource($"{type.Name}.BoundedContextDescriptor.g.cs", SourceText.From(
            GenerateSource(type, name!, capabilities, adapters, factoryMethodName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Capability> GetCapabilities(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<Capability>();
        foreach (var attr in type.GetAttributes().Where(static attr => attr.AttributeClass?.ToDisplayString() == CapabilityAttributeName))
        {
            var name = attr.ConstructorArguments[0].Value as string;
            var serviceType = attr.ConstructorArguments[1].Value as ITypeSymbol;
            if (!string.IsNullOrWhiteSpace(name) && serviceType is not null)
                builder.Add(new Capability(name!, serviceType, attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<Adapter> GetAdapters(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<Adapter>();
        foreach (var attr in type.GetAttributes().Where(static attr => attr.AttributeClass?.ToDisplayString() == AdapterAttributeName))
        {
            var upstream = attr.ConstructorArguments[0].Value as string;
            var downstream = attr.ConstructorArguments[1].Value as string;
            var sourceType = attr.ConstructorArguments[2].Value as ITypeSymbol;
            var targetType = attr.ConstructorArguments[3].Value as ITypeSymbol;
            if (!string.IsNullOrWhiteSpace(upstream)
                && !string.IsNullOrWhiteSpace(downstream)
                && sourceType is not null
                && targetType is not null)
            {
                builder.Add(new Adapter(
                    upstream!,
                    downstream!,
                    sourceType,
                    targetType,
                    $"{upstream}->{downstream}:{sourceType.ToDisplayString(TypeFormat)}:{targetType.ToDisplayString(TypeFormat)}",
                    attr.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
            }
        }

        return builder.ToImmutable();
    }

    private static bool TryFindDuplicateCapability(IReadOnlyList<Capability> capabilities, out Capability duplicate)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var capability in capabilities)
        {
            if (!seen.Add(capability.Name))
            {
                duplicate = capability;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static bool TryFindDuplicateAdapter(IReadOnlyList<Adapter> adapters, out Adapter duplicate)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var adapter in adapters)
        {
            if (!seen.Add(adapter.Key))
            {
                duplicate = adapter;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        string contextName,
        IReadOnlyList<Capability> capabilities,
        IReadOnlyList<Adapter> adapters,
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
        sb.Append("    public static global::PatternKit.Application.BoundedContexts.BoundedContextDescriptor ")
            .Append(factoryMethodName)
            .AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Application.BoundedContexts.BoundedContextDescriptor.Create(\"")
            .Append(Escape(contextName))
            .AppendLine("\");");

        foreach (var capability in capabilities.OrderBy(static capability => capability.Name, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.AddCapability(\"")
                .Append(Escape(capability.Name))
                .Append("\", typeof(")
                .Append(capability.ServiceType.ToDisplayString(TypeFormat))
                .AppendLine("));");
        }

        foreach (var adapter in adapters.OrderBy(static adapter => adapter.Key, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.AddAdapter(\"")
                .Append(Escape(adapter.UpstreamContext))
                .Append("\", \"")
                .Append(Escape(adapter.DownstreamContext))
                .Append("\", typeof(")
                .Append(adapter.SourceType.ToDisplayString(TypeFormat))
                .Append("), typeof(")
                .Append(adapter.TargetType.ToDisplayString(TypeFormat))
                .AppendLine("));");
        }

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private readonly record struct Capability(string Name, ITypeSymbol ServiceType, Location? Location);

    private readonly record struct Adapter(
        string UpstreamContext,
        string DownstreamContext,
        ITypeSymbol SourceType,
        ITypeSymbol TargetType,
        string Key,
        Location? Location);
}
