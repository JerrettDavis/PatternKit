using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class MessageFilterGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKMF001",
        "Message filter type must be partial",
        "Type '{0}' is marked with [GenerateMessageFilter] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingRules = new(
        "PKMF002",
        "Message filter has no rules",
        "Type '{0}' is marked with [GenerateMessageFilter] but does not declare any [MessageFilterRule] methods",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRule = new(
        "PKMF003",
        "Message filter rule signature is invalid",
        "Message filter rule '{0}' must be static and return bool with Message<TPayload> and MessageContext parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRule = new(
        "PKMF004",
        "Message filter rule name or order is duplicated",
        "Message filter rule '{0}' duplicates another rule name or order in '{1}'",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateMessageFilterAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateMessageFilterAttribute");
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

        var payloadType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (payloadType is null)
            return;

        var hasRuleAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.MessageFilterRuleAttribute"));
        var rules = GetRules(type, payloadType, context);
        if (rules.Length == 0)
        {
            if (!hasRuleAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingRules, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (HasDuplicates(rules, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRule, duplicate.Location, duplicate.Name, type.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var filterName = GetNamedString(attribute, "FilterName") ?? "message-filter";
        var rejectionReason = GetNamedString(attribute, "RejectionReason") ?? "Message did not match any allow rule.";
        var ordered = rules.OrderBy(static rule => rule.Order).ThenBy(static rule => rule.Name).ToArray();

        context.AddSource($"{type.Name}.MessageFilter.g.cs", SourceText.From(
            GenerateSource(type, payloadType, ordered, factoryName, filterName, rejectionReason),
            Encoding.UTF8));
    }

    private static ImmutableArray<Rule> GetRules(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Rule>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.MessageFilterRuleAttribute");
            if (attr is null)
                continue;

            if (!TryGetRule(method, payloadType, attr, out var rule))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRule, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(rule);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetRule(
        IMethodSymbol method,
        INamedTypeSymbol payloadType,
        AttributeData attribute,
        out Rule rule)
    {
        rule = default;
        if (!IsRule(method, payloadType) || attribute.ConstructorArguments.Length != 2)
            return false;

        var name = attribute.ConstructorArguments[0].Value as string;
        var order = attribute.ConstructorArguments[1].Value as int? ?? 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        rule = new Rule(name!, order, method.Name, method.Locations.FirstOrDefault());
        return true;
    }

    private static bool IsRule(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 2 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsMessageOfPayload(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool HasDuplicates(IReadOnlyList<Rule> rules, out Rule duplicate)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        var orders = new HashSet<int>();
        foreach (var rule in rules)
        {
            if (!names.Add(rule.Name) || !orders.Add(rule.Order))
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
        INamedTypeSymbol payloadType,
        IReadOnlyList<Rule> rules,
        string factoryName,
        string filterName,
        string rejectionReason)
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
        sb.Append("    public static global::PatternKit.Messaging.Routing.MessageFilter<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Routing.MessageFilter<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">.Create(")
            .Append(ToLiteral(filterName))
            .AppendLine(")");

        foreach (var rule in rules)
            sb.Append("            .AllowWhen(").Append(ToLiteral(rule.Name)).Append(", ").Append(rule.MethodName).AppendLine(")");

        sb.Append("            .RejectUnmatched(").Append(ToLiteral(rejectionReason)).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string ToLiteral(string value)
        => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private readonly record struct Rule(string Name, int Order, string MethodName, Location? Location);
}
