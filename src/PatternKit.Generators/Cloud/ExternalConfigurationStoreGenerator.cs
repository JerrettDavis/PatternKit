using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Cloud;

[Generator]
public sealed class ExternalConfigurationStoreGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKECS001",
        "External configuration store type must be partial",
        "Type '{0}' is marked with [GenerateExternalConfigurationStore] but is not declared as partial",
        "PatternKit.Generators.Cloud",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidLoader = new(
        "PKECS002",
        "External configuration store loader is invalid",
        "Type '{0}' must declare exactly one static [ExternalConfigurationLoader] method returning ValueTask<ExternalConfigurationSnapshot<TSettings>> with a CancellationToken parameter",
        "PatternKit.Generators.Cloud",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidValidator = new(
        "PKECS003",
        "External configuration store validator signature is invalid",
        "External configuration validator '{0}' must be static and return bool with a TSettings parameter",
        "PatternKit.Generators.Cloud",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateValidator = new(
        "PKECS004",
        "External configuration store validator order is duplicated",
        "External configuration validator '{0}' duplicates another validator order in '{1}'",
        "PatternKit.Generators.Cloud",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Cloud.GenerateExternalConfigurationStoreAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Cloud.GenerateExternalConfigurationStoreAttribute");
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

        var settingsType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (settingsType is null)
            return;

        var loaders = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Cloud.ExternalConfigurationLoaderAttribute"))
            .ToArray();
        if (loaders.Length != 1 || !IsLoader(loaders[0], settingsType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidLoader, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var validators = GetValidators(type, settingsType, context);
        if (HasDuplicates(validators, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateValidator, duplicate.Location, duplicate.MethodName, type.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var storeName = GetNamedString(attribute, "StoreName") ?? "external-configuration-store";
        var cacheMilliseconds = GetNamedInt(attribute, "CacheMilliseconds");
        var ordered = validators.OrderBy(static validator => validator.Order).ToArray();

        context.AddSource($"{type.Name}.ExternalConfigurationStore.g.cs", SourceText.From(
            GenerateSource(type, settingsType, loaders[0].Name, ordered, factoryName, storeName, cacheMilliseconds),
            Encoding.UTF8));
    }

    private static ImmutableArray<Validator> GetValidators(
        INamedTypeSymbol type,
        INamedTypeSymbol settingsType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Validator>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Cloud.ExternalConfigurationValidatorAttribute");
            if (attr is null)
                continue;

            if (!IsValidator(method, settingsType) || attr.ConstructorArguments.Length != 2)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidValidator, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            var reason = attr.ConstructorArguments[0].Value as string;
            var order = attr.ConstructorArguments[1].Value as int? ?? 0;
            if (string.IsNullOrWhiteSpace(reason))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidValidator, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(new Validator(reason!, order, method.Name, method.Locations.FirstOrDefault()));
        }

        return builder.ToImmutable();
    }

    private static bool IsLoader(IMethodSymbol method, INamedTypeSymbol settingsType)
        => method.IsStatic &&
           method.Parameters.Length == 1 &&
           method.Parameters[0].Type.ToDisplayString() == "System.Threading.CancellationToken" &&
           method.ReturnType is INamedTypeSymbol returnType &&
           returnType.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>" &&
           returnType.TypeArguments[0] is INamedTypeSymbol snapshot &&
           snapshot.ConstructedFrom.ToDisplayString() == "PatternKit.Cloud.ExternalConfigurationStore.ExternalConfigurationSnapshot<TSettings>" &&
           SymbolEqualityComparer.Default.Equals(snapshot.TypeArguments[0], settingsType);

    private static bool IsValidator(IMethodSymbol method, INamedTypeSymbol settingsType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, settingsType);

    private static bool HasDuplicates(IReadOnlyList<Validator> validators, out Validator duplicate)
    {
        var orders = new HashSet<int>();
        foreach (var validator in validators)
        {
            if (!orders.Add(validator.Order))
            {
                duplicate = validator;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol settingsType,
        string loaderName,
        IReadOnlyList<Validator> validators,
        string factoryName,
        string storeName,
        int cacheMilliseconds)
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
        sb.Append("    public static global::PatternKit.Cloud.ExternalConfigurationStore.ExternalConfigurationStore<")
            .Append(settingsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append("        => global::PatternKit.Cloud.ExternalConfigurationStore.ExternalConfigurationStore<")
            .Append(settingsType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">.Create(")
            .Append(ToLiteral(storeName))
            .AppendLine(")");
        sb.Append("            .LoadFrom(").Append(loaderName).AppendLine(")");

        foreach (var validator in validators)
            sb.Append("            .ValidateWith(").Append(ToLiteral(validator.RejectionReason)).Append(", ").Append(validator.MethodName).AppendLine(")");

        if (cacheMilliseconds > 0)
            sb.Append("            .CacheFor(global::System.TimeSpan.FromMilliseconds(").Append(cacheMilliseconds).AppendLine("))");

        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static int GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int? ?? 0;

    private static string ToLiteral(string value)
        => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private readonly record struct Validator(string RejectionReason, int Order, string MethodName, Location? Location);
}
