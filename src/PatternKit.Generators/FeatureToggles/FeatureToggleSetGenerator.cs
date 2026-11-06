using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.FeatureToggles;

[Generator]
public sealed class FeatureToggleSetGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.FeatureToggles.GenerateFeatureToggleSetAttribute";
    private const string RuleAttributeName = "PatternKit.Generators.FeatureToggles.FeatureToggleRuleAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKFT001", "Feature Toggle host must be partial",
        "Type '{0}' is marked with [GenerateFeatureToggleSet] but is not declared as partial",
        "PatternKit.Generators.FeatureToggles", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingRules = new(
        "PKFT002", "Feature Toggle rules are missing",
        "Feature Toggle set '{0}' must declare at least one [FeatureToggleRule] method",
        "PatternKit.Generators.FeatureToggles", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidRule = new(
        "PKFT003", "Feature Toggle rule signature is invalid",
        "Feature Toggle rule '{0}' must be static and return bool from one context parameter",
        "PatternKit.Generators.FeatureToggles", DiagnosticSeverity.Error, true);

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

        var contextType = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (contextType is null)
            return;

        var rules = FindRules(type, contextType).ToArray();
        if (rules.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingRules, node.Identifier.GetLocation(), type.Name));
            return;
        }

        foreach (var rule in rules)
        {
            if (!rule.Valid)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRule, rule.Method.Locations.FirstOrDefault(), rule.Method.Name));
                return;
            }
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var setName = GetNamedString(attribute, "SetName") ?? "feature-toggles";
        context.AddSource($"{type.Name}.FeatureToggleSet.g.cs", SourceText.From(
            GenerateSource(type, contextType, rules, factoryName, setName),
            Encoding.UTF8));
    }

    private static IEnumerable<RuleModel> FindRules(INamedTypeSymbol hostType, INamedTypeSymbol contextType)
    {
        foreach (var method in hostType.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == RuleAttributeName);
            if (attr is null)
                continue;

            var name = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
            var defaultEnabled = GetNamedBool(attr, "DefaultEnabled") ?? false;
            var valid = method.IsStatic
                && method.ReturnType.SpecialType == SpecialType.System_Boolean
                && method.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType);
            yield return new RuleModel(method, name ?? method.Name, defaultEnabled, valid);
        }
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol contextType,
        IReadOnlyList<RuleModel> rules,
        string factoryName,
        string setName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var contextTypeName = contextType.ToDisplayString(TypeFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        var containingTypes = GetContainingTypes(type);
        var indentLevel = 0;
        foreach (var containingType in containingTypes)
        {
            AppendTypeDeclaration(sb, containingType, indentLevel);
            sb.AppendLine();
            sb.AppendLine(new string(' ', indentLevel * 4) + "{");
            indentLevel++;
        }

        AppendTypeDeclaration(sb, type, indentLevel);
        sb.AppendLine();
        var indent = new string(' ', indentLevel * 4);
        sb.AppendLine(indent + "{");
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.FeatureToggles.FeatureToggleSet<").Append(contextTypeName).Append("> ").Append(factoryName).AppendLine("()");
        sb.AppendLine(memberIndent + "{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Application.FeatureToggles.FeatureToggleSet<").Append(contextTypeName).Append(">.Create(\"").Append(Escape(setName)).AppendLine("\")");
        foreach (var rule in rules)
        {
            sb.Append(bodyIndent).Append("    .AddRule(\"").Append(Escape(rule.Name)).Append("\", ").Append(rule.DefaultEnabled ? "true" : "false").Append(", ").Append(rule.Method.Name).AppendLine(")");
        }

        sb.Append(bodyIndent).AppendLine("    .Build();");
        sb.AppendLine(memberIndent + "}");
        sb.AppendLine(indent + "}");
        for (var i = containingTypes.Length - 1; i >= 0; i--)
        {
            sb.AppendLine(new string(' ', i * 4) + "}");
        }

        return sb.ToString();
    }

    private static INamedTypeSymbol[] GetContainingTypes(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        return containingTypes.ToArray();
    }

    private static void AppendTypeDeclaration(StringBuilder sb, INamedTypeSymbol type, int indentLevel)
    {
        sb.Append(new string(' ', indentLevel * 4));
        sb.Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name);
    }

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

    private static bool? GetNamedBool(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as bool?;

    private sealed class RuleModel
    {
        public RuleModel(IMethodSymbol method, string name, bool defaultEnabled, bool valid)
        {
            Method = method;
            Name = name;
            DefaultEnabled = defaultEnabled;
            Valid = valid;
        }

        public IMethodSymbol Method { get; }

        public string Name { get; }

        public bool DefaultEnabled { get; }

        public bool Valid { get; }
    }
}
