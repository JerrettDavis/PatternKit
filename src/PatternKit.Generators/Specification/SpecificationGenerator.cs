using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Specification;

[Generator]
public sealed class SpecificationGenerator : IIncrementalGenerator
{
    private const string GenerateSpecificationRegistryAttributeName = "PatternKit.Generators.Specification.GenerateSpecificationRegistryAttribute";
    private const string SpecificationRuleAttributeName = "PatternKit.Generators.Specification.SpecificationRuleAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKSPEC001",
        "Specification registry host must be partial",
        "Type '{0}' is marked with [GenerateSpecificationRegistry] but is not declared as partial",
        "PatternKit.Generators.Specification",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingRules = new(
        "PKSPEC002",
        "Specification registry has no rules",
        "Type '{0}' is marked with [GenerateSpecificationRegistry] but does not declare any specification rules",
        "PatternKit.Generators.Specification",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRule = new(
        "PKSPEC003",
        "Specification rule signature is invalid",
        "Rule method '{0}' must be static, return bool, and accept exactly one candidate parameter",
        "PatternKit.Generators.Specification",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRule = new(
        "PKSPEC004",
        "Specification rule is duplicated",
        "Specification rule '{0}' is registered more than once",
        "PatternKit.Generators.Specification",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateSpecificationRegistryAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == GenerateSpecificationRegistryAttributeName);
            if (attr is not null)
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

        var candidateType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (candidateType is null)
            return;

        var rules = GetRules(type, candidateType, context, out var hasAnnotatedRules);
        if (!hasAnnotatedRules)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingRules, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (rules.Length == 0)
            return;

        if (TryFindDuplicate(rules, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRule, duplicate.Location, duplicate.Name));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        context.AddSource($"{type.Name}.SpecificationRegistry.g.cs", SourceText.From(
            GenerateSource(type, candidateType, rules, factoryMethodName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Rule> GetRules(
        INamedTypeSymbol type,
        INamedTypeSymbol candidateType,
        SourceProductionContext context,
        out bool hasAnnotatedRules)
    {
        hasAnnotatedRules = false;
        var builder = ImmutableArray.CreateBuilder<Rule>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attr in method.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != SpecificationRuleAttributeName)
                    continue;

                hasAnnotatedRules = true;
                if (!TryGetRule(method, attr, candidateType, out var rule))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidRule, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                builder.Add(rule);
            }
        }

        return builder.ToImmutable();
    }

    private static bool TryGetRule(
        IMethodSymbol method,
        AttributeData attribute,
        INamedTypeSymbol candidateType,
        out Rule rule)
    {
        rule = default;
        var name = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!method.IsStatic || method.IsGenericMethod || method.ReturnsVoid || method.ReturnType.SpecialType != SpecialType.System_Boolean)
            return false;

        if (method.Parameters.Length != 1 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, candidateType))
            return false;

        rule = new Rule(name!, method.Name, method.Locations.FirstOrDefault());
        return true;
    }

    private static bool TryFindDuplicate(IReadOnlyList<Rule> rules, out Rule duplicate)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!seen.Add(rule.Name))
            {
                duplicate = rule;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol candidateType,
        IReadOnlyList<Rule> rules,
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
        sb.Append("    public static global::PatternKit.Application.Specification.SpecificationRegistry<")
            .Append(candidateType.ToDisplayString(TypeFormat))
            .Append("> ")
            .Append(factoryMethodName)
            .AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Application.Specification.SpecificationRegistry<")
            .Append(candidateType.ToDisplayString(TypeFormat))
            .AppendLine(">.Create();");

        foreach (var rule in rules.OrderBy(static rule => rule.Name, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.Add(\"")
                .Append(Escape(rule.Name))
                .Append("\", static candidate => ")
                .Append(rule.MethodName)
                .AppendLine("(candidate));");
        }

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private readonly record struct Rule(string Name, string MethodName, Location? Location);
}
