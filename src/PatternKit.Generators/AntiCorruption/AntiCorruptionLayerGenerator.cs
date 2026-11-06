using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.AntiCorruption;

[Generator]
public sealed class AntiCorruptionLayerGenerator : IIncrementalGenerator
{
    private const string GenerateAntiCorruptionLayerAttributeName = "PatternKit.Generators.AntiCorruption.GenerateAntiCorruptionLayerAttribute";
    private const string AntiCorruptionTranslatorAttributeName = "PatternKit.Generators.AntiCorruption.AntiCorruptionTranslatorAttribute";
    private const string AntiCorruptionExternalRuleAttributeName = "PatternKit.Generators.AntiCorruption.AntiCorruptionExternalRuleAttribute";
    private const string AntiCorruptionDomainRuleAttributeName = "PatternKit.Generators.AntiCorruption.AntiCorruptionDomainRuleAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKACL001",
        "Anti-corruption layer host must be partial",
        "Type '{0}' is marked with [GenerateAntiCorruptionLayer] but is not declared as partial",
        "PatternKit.Generators.AntiCorruption",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingTranslator = new(
        "PKACL002",
        "Anti-corruption layer translator is missing",
        "Anti-corruption layer '{0}' must declare exactly one translator",
        "PatternKit.Generators.AntiCorruption",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidTranslator = new(
        "PKACL003",
        "Anti-corruption layer translator signature is invalid",
        "Translator method '{0}' must be static, return the domain type, and accept exactly one external parameter",
        "PatternKit.Generators.AntiCorruption",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRule = new(
        "PKACL004",
        "Anti-corruption layer validation rule signature is invalid",
        "Validation rule method '{0}' must be static, return bool, and accept exactly one matching model parameter",
        "PatternKit.Generators.AntiCorruption",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateAntiCorruptionLayerAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == GenerateAntiCorruptionLayerAttributeName);
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

        var externalType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        var domainType = attribute.ConstructorArguments.Length >= 2
            ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol
            : null;
        if (externalType is null || domainType is null)
            return;

        var translators = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => HasAttribute(method, AntiCorruptionTranslatorAttributeName))
            .ToArray();
        if (translators.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingTranslator, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var translator = translators[0];
        if (!IsTranslator(translator, externalType, domainType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidTranslator, translator.Locations.FirstOrDefault(), translator.Name));
            return;
        }

        var externalRules = GetRules(type, AntiCorruptionExternalRuleAttributeName, externalType, context);
        var domainRules = GetRules(type, AntiCorruptionDomainRuleAttributeName, domainType, context);
        if (externalRules.IsDefault || domainRules.IsDefault)
            return;

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        var layerName = GetNamedString(attribute, "LayerName") ?? "anti-corruption-layer";
        var sourceSystem = GetNamedString(attribute, "SourceSystem") ?? "external";
        context.AddSource($"{type.Name}.AntiCorruptionLayer.g.cs", SourceText.From(
            GenerateSource(type, externalType, domainType, translator.Name, externalRules, domainRules, factoryMethodName, layerName, sourceSystem),
            Encoding.UTF8));
    }

    private static ImmutableArray<Rule> GetRules(
        INamedTypeSymbol type,
        string attributeName,
        INamedTypeSymbol modelType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Rule>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attr in method.GetAttributes().Where(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            {
                if (!IsRule(method, modelType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidRule, method.Locations.FirstOrDefault(), method.Name));
                    return default;
                }

                var reason = attr.ConstructorArguments.Length == 1
                    ? attr.ConstructorArguments[0].Value as string
                    : null;
                builder.Add(new Rule(method.Name, reason ?? "Validation rule rejected the model."));
            }
        }

        return builder.ToImmutable();
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol externalType,
        INamedTypeSymbol domainType,
        string translator,
        IReadOnlyList<Rule> externalRules,
        IReadOnlyList<Rule> domainRules,
        string factoryMethodName,
        string layerName,
        string sourceSystem)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var externalTypeName = externalType.ToDisplayString(TypeFormat);
        var domainTypeName = domainType.ToDisplayString(TypeFormat);
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
        sb.Append("    public static global::PatternKit.Application.AntiCorruption.AntiCorruptionLayer<")
            .Append(externalTypeName)
            .Append(", ")
            .Append(domainTypeName)
            .Append("> ")
            .Append(factoryMethodName)
            .AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Application.AntiCorruption.AntiCorruptionLayer<")
            .Append(externalTypeName)
            .Append(", ")
            .Append(domainTypeName)
            .Append(">.Create(\"")
            .Append(Escape(layerName))
            .AppendLine("\")");
        sb.Append("            .FromSource(\"").Append(Escape(sourceSystem)).AppendLine("\")");
        sb.Append("            .TranslateWith(static external => ").Append(translator).AppendLine("(external));");

        foreach (var rule in externalRules)
            sb.Append("        builder.RequireExternal(static external => ").Append(rule.MethodName).Append("(external), \"").Append(Escape(rule.RejectionReason)).AppendLine("\");");
        foreach (var rule in domainRules)
            sb.Append("        builder.RequireDomain(static domain => ").Append(rule.MethodName).Append("(domain), \"").Append(Escape(rule.RejectionReason)).AppendLine("\");");

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsTranslator(IMethodSymbol method, ITypeSymbol externalType, ITypeSymbol domainType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, externalType)
        && SymbolEqualityComparer.Default.Equals(method.ReturnType, domainType);

    private static bool IsRule(IMethodSymbol method, ITypeSymbol modelType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.ReturnType.SpecialType == SpecialType.System_Boolean
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, modelType);

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

    private readonly record struct Rule(string MethodName, string RejectionReason);
}
