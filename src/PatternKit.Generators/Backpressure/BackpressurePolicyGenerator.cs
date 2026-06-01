using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Backpressure;

[Generator]
public sealed class BackpressurePolicyGenerator : IIncrementalGenerator
{
    private const string GenerateBackpressurePolicyAttributeName = "PatternKit.Generators.Backpressure.GenerateBackpressurePolicyAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKBP001",
        "Backpressure policy host must be partial",
        "Type '{0}' is marked with [GenerateBackpressurePolicy] but is not declared as partial",
        "PatternKit.Generators.Backpressure",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKBP002",
        "Backpressure policy configuration is invalid",
        "Backpressure policy '{0}' must have Capacity >= 1 and WaitTimeoutMilliseconds >= 0",
        "PatternKit.Generators.Backpressure",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidFactoryMethodName = new(
        "PKBP003",
        "Backpressure factory method name is invalid",
        "Backpressure policy '{0}' has an invalid factory method name '{1}'",
        "PatternKit.Generators.Backpressure",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidMode = new(
        "PKBP004",
        "Backpressure mode is invalid",
        "Backpressure policy '{0}' has an invalid mode '{1}'",
        "PatternKit.Generators.Backpressure",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateBackpressurePolicyAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == GenerateBackpressurePolicyAttributeName);
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

        var resultType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (resultType is null)
            return;

        var capacity = GetNamedInt(attribute, "Capacity") ?? 8;
        var waitTimeoutMilliseconds = GetNamedInt(attribute, "WaitTimeoutMilliseconds") ?? 0;
        if (capacity < 1 || waitTimeoutMilliseconds < 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        if (!IsIdentifier(factoryMethodName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidFactoryMethodName, node.Identifier.GetLocation(), type.Name, factoryMethodName));
            return;
        }

        var policyName = GetNamedString(attribute, "PolicyName") ?? "backpressure";
        var mode = GetNamedString(attribute, "Mode") ?? "Reject";
        if (!IsKnownMode(mode))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMode, node.Identifier.GetLocation(), type.Name, mode));
            return;
        }

        context.AddSource($"{type.Name}.BackpressurePolicy.g.cs", SourceText.From(
            GenerateSource(type, resultType, factoryMethodName, policyName, capacity, mode, waitTimeoutMilliseconds),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol resultType,
        string factoryMethodName,
        string policyName,
        int capacity,
        string mode,
        int waitTimeoutMilliseconds)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var resultTypeName = resultType.ToDisplayString(TypeFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Messaging.Reliability.Backpressure.BackpressurePolicy<").Append(resultTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine(memberIndent + "{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Messaging.Reliability.Backpressure.BackpressurePolicy<").Append(resultTypeName).Append(">.Create(\"").Append(Escape(policyName)).AppendLine("\")");
        sb.Append(bodyIndent).Append("    .WithCapacity(").Append(capacity).AppendLine(")");
        sb.Append(bodyIndent).Append("    .WithMode(global::PatternKit.Messaging.Reliability.Backpressure.BackpressureMode.").Append(mode).AppendLine(")");
        sb.Append(bodyIndent).Append("    .WithWaitTimeout(global::System.TimeSpan.FromMilliseconds(").Append(waitTimeoutMilliseconds).AppendLine("))");
        sb.Append(bodyIndent).AppendLine("    .Build();");
        sb.AppendLine(memberIndent + "}");
        sb.AppendLine(indent + "}");
        for (var i = containingTypes.Length - 1; i >= 0; i--)
            sb.AppendLine(new string(' ', i * 4) + "}");

        return sb.ToString();
    }

    private static INamedTypeSymbol[] GetContainingTypes(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
            containingTypes.Push(current);

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

    private static bool IsIdentifier(string value)
        => SyntaxFacts.IsValidIdentifier(value) && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;

    private static bool IsKnownMode(string value)
        => value is "Reject" or "Wait" or "DropNewest" or "DropOldest" or "Shed" or "Observe";

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

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;

}
