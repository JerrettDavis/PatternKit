using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.AuditLog;

[Generator]
public sealed class AuditLogGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.AuditLog.GenerateAuditLogAttribute";
    private const string KeySelectorAttributeName = "PatternKit.Generators.AuditLog.AuditLogKeySelectorAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKAUD001", "Audit Log host must be partial",
        "Type '{0}' is marked with [GenerateAuditLog] but is not declared as partial",
        "PatternKit.Generators.AuditLog", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingKeySelector = new(
        "PKAUD002", "Audit Log key selector is missing",
        "Audit Log '{0}' must declare exactly one [AuditLogKeySelector] method",
        "PatternKit.Generators.AuditLog", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidKeySelector = new(
        "PKAUD003", "Audit Log key selector signature is invalid",
        "Audit Log key selector '{0}' must be static and return TKey from one TEntry parameter",
        "PatternKit.Generators.AuditLog", DiagnosticSeverity.Error, true);

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

        var entryType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var keyType = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (entryType is null || keyType is null)
            return;

        var selectors = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == KeySelectorAttributeName))
            .ToArray();
        if (selectors.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingKeySelector, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var selector = selectors[0];
        if (!selector.IsStatic
            || selector.IsGenericMethod
            || selector.Parameters.Length != 1
            || !SymbolEqualityComparer.Default.Equals(selector.Parameters[0].Type, entryType)
            || !SymbolEqualityComparer.Default.Equals(selector.ReturnType, keyType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidKeySelector, selector.Locations.FirstOrDefault(), selector.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var logName = GetNamedString(attribute, "LogName") ?? "audit-log";
        context.AddSource($"{type.Name}.AuditLog.g.cs", SourceText.From(
            GenerateSource(type, entryType, keyType, selector.Name, factoryName, logName),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol entryType,
        INamedTypeSymbol keyType,
        string selectorName,
        string factoryName,
        string logName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var entryTypeName = entryType.ToDisplayString(TypeFormat);
        var keyTypeName = keyType.ToDisplayString(TypeFormat);
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
        var indent = new string(' ', indentLevel * 4);
        sb.AppendLine();
        sb.AppendLine(indent + "{");
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.AuditLog.InMemoryAuditLog<")
            .Append(entryTypeName).Append(", ").Append(keyTypeName).Append("> ").Append(factoryName).AppendLine("()");
        sb.Append(bodyIndent).Append("=> global::PatternKit.Application.AuditLog.InMemoryAuditLog<")
            .Append(entryTypeName).Append(", ").Append(keyTypeName).Append(">.Create(\"").Append(Escape(logName)).Append("\", ").Append(selectorName).AppendLine(").Build();");
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
}
