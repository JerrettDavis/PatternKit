using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Interpreter;

[Generator]
public sealed class InterpreterGenerator : IIncrementalGenerator
{
    private const string GenerateInterpreterAttributeName = "PatternKit.Generators.Interpreter.GenerateInterpreterAttribute";
    private const string InterpreterTerminalAttributeName = "PatternKit.Generators.Interpreter.InterpreterTerminalAttribute";
    private const string InterpreterNonTerminalAttributeName = "PatternKit.Generators.Interpreter.InterpreterNonTerminalAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKINT001",
        "Interpreter host must be partial",
        "Type '{0}' is marked with [GenerateInterpreter] but is not declared as partial",
        "PatternKit.Generators.Interpreter",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingRules = new(
        "PKINT002",
        "Interpreter has no rules",
        "Type '{0}' is marked with [GenerateInterpreter] but does not declare any terminal or non-terminal rules",
        "PatternKit.Generators.Interpreter",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRule = new(
        "PKINT003",
        "Interpreter rule signature is invalid",
        "Rule method '{0}' must be static and return the configured result type with either a terminal signature (string token[, context]) or non-terminal signature (result[] args[, context])",
        "PatternKit.Generators.Interpreter",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRule = new(
        "PKINT004",
        "Interpreter rule is duplicated",
        "Interpreter rule '{0}' is registered more than once as a {1}",
        "PatternKit.Generators.Interpreter",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateInterpreterAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == GenerateInterpreterAttributeName);
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

        var contextType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        var resultType = attribute.ConstructorArguments.Length >= 2
            ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol
            : null;
        if (contextType is null || resultType is null)
            return;

        var rules = GetRules(type, contextType, resultType, context);
        if (rules.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingRules, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (TryFindDuplicate(rules, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRule, duplicate.Location, duplicate.Name, duplicate.KindText));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        context.AddSource($"{type.Name}.Interpreter.g.cs", SourceText.From(
            GenerateSource(type, contextType, resultType, rules, factoryMethodName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Rule> GetRules(
        INamedTypeSymbol type,
        INamedTypeSymbol contextType,
        INamedTypeSymbol resultType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Rule>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attr in method.GetAttributes())
            {
                var attrName = attr.AttributeClass?.ToDisplayString();
                if (attrName != InterpreterTerminalAttributeName && attrName != InterpreterNonTerminalAttributeName)
                    continue;

                var isTerminal = attrName == InterpreterTerminalAttributeName;
                if (!TryGetRule(method, attr, isTerminal, contextType, resultType, out var rule))
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
        bool isTerminal,
        INamedTypeSymbol contextType,
        INamedTypeSymbol resultType,
        out Rule rule)
    {
        rule = default;
        var name = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!method.IsStatic || method.IsGenericMethod || method.ReturnsVoid)
            return false;

        if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, resultType))
            return false;

        var parameters = method.Parameters;
        if (parameters.Length is < 1 or > 2)
            return false;

        var firstParameterValid = isTerminal
            ? parameters[0].Type.SpecialType == SpecialType.System_String
            : parameters[0].Type is IArrayTypeSymbol arrayType && SymbolEqualityComparer.Default.Equals(arrayType.ElementType, resultType);
        if (!firstParameterValid)
            return false;

        if (parameters.Length == 2 && !SymbolEqualityComparer.Default.Equals(parameters[1].Type, contextType))
            return false;

        rule = new Rule(
            name!,
            isTerminal,
            method.Name,
            parameters.Length == 2,
            method.Locations.FirstOrDefault());
        return true;
    }

    private static bool TryFindDuplicate(IReadOnlyList<Rule> rules, out Rule duplicate)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            var key = rule.KindText + ":" + rule.Name;
            if (!seen.Add(key))
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
        INamedTypeSymbol contextType,
        INamedTypeSymbol resultType,
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
        sb.Append("    public static global::PatternKit.Behavioral.Interpreter.Interpreter<")
            .Append(contextType.ToDisplayString(TypeFormat))
            .Append(", ")
            .Append(resultType.ToDisplayString(TypeFormat))
            .Append("> ")
            .Append(factoryMethodName)
            .AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Behavioral.Interpreter.Interpreter.Create<")
            .Append(contextType.ToDisplayString(TypeFormat))
            .Append(", ")
            .Append(resultType.ToDisplayString(TypeFormat))
            .AppendLine(">();");

        foreach (var rule in rules.Where(static rule => rule.IsTerminal).OrderBy(static rule => rule.Name, System.StringComparer.Ordinal))
            EmitRule(sb, rule, "Terminal", "token");

        foreach (var rule in rules.Where(static rule => !rule.IsTerminal).OrderBy(static rule => rule.Name, System.StringComparer.Ordinal))
            EmitRule(sb, rule, "NonTerminal", "args");

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitRule(StringBuilder sb, Rule rule, string builderMethodName, string firstArgumentName)
    {
        sb.Append("        builder.")
            .Append(builderMethodName)
            .Append("(\"")
            .Append(Escape(rule.Name))
            .Append("\", static (")
            .Append(firstArgumentName)
            .Append(", context) => ")
            .Append(rule.MethodName)
            .Append('(')
            .Append(firstArgumentName);

        if (rule.UsesContext)
            sb.Append(", context");

        sb.AppendLine("));");
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private readonly record struct Rule(
        string Name,
        bool IsTerminal,
        string MethodName,
        bool UsesContext,
        Location? Location)
    {
        public string KindText => IsTerminal ? "terminal" : "non-terminal";
    }
}
