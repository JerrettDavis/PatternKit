using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.EventCarriedStateTransfer;

[Generator]
public sealed class EventCarriedStateTransferGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.EventCarriedStateTransfer.GenerateEventCarriedStateTransferAttribute";
    private const string KeyAttributeName = "PatternKit.Generators.EventCarriedStateTransfer.EventCarriedStateKeyAttribute";
    private const string VersionAttributeName = "PatternKit.Generators.EventCarriedStateTransfer.EventCarriedStateVersionAttribute";
    private const string MapperAttributeName = "PatternKit.Generators.EventCarriedStateTransfer.EventCarriedStateMapperAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKECST001", "Event-Carried State Transfer host must be partial",
        "Type '{0}' is marked with [GenerateEventCarriedStateTransfer] but is not declared as partial",
        "PatternKit.Generators.EventCarriedStateTransfer", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMember = new(
        "PKECST002", "Event-Carried State Transfer methods are missing",
        "Event-Carried State Transfer type '{0}' must declare exactly one key selector, one version selector, and one state mapper",
        "PatternKit.Generators.EventCarriedStateTransfer", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKECST003", "Event-Carried State Transfer method signature is invalid",
        "Event-Carried State Transfer method '{0}' has an invalid static signature for the configured event, key, version, or state type",
        "PatternKit.Generators.EventCarriedStateTransfer", DiagnosticSeverity.Error, true);

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == AttributeName);
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

        var eventType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var keyType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        var stateType = attribute.ConstructorArguments.Length >= 3 ? attribute.ConstructorArguments[2].Value as INamedTypeSymbol : null;
        if (eventType is null || keyType is null || stateType is null)
            return;

        var keySelectors = MembersWith(type, KeyAttributeName);
        var versionSelectors = MembersWith(type, VersionAttributeName);
        var mappers = MembersWith(type, MapperAttributeName);
        if (keySelectors.Length != 1 || versionSelectors.Length != 1 || mappers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMember, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (!IsSelector(keySelectors[0], eventType, keyType) ||
            !IsVersionSelector(versionSelectors[0], eventType) ||
            !IsSelector(mappers[0], eventType, stateType))
        {
            var invalid =
                !IsSelector(keySelectors[0], eventType, keyType) ? keySelectors[0] :
                !IsVersionSelector(versionSelectors[0], eventType) ? versionSelectors[0] :
                mappers[0];
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalid.Locations.FirstOrDefault(), invalid.Name));
            return;
        }

        context.AddSource($"{type.Name}.EventCarriedStateTransfer.g.cs", SourceText.From(GenerateSource(
            type,
            eventType,
            keyType,
            stateType,
            keySelectors[0].Name,
            versionSelectors[0].Name,
            mappers[0].Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "TransferName") ?? "event-carried-state-transfer"), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static bool IsSelector(IMethodSymbol method, INamedTypeSymbol eventType, ITypeSymbol returnType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, returnType) &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, eventType);

    private static bool IsVersionSelector(IMethodSymbol method, INamedTypeSymbol eventType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Int64 &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, eventType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol eventType,
        INamedTypeSymbol keyType,
        INamedTypeSymbol stateType,
        string keySelectorName,
        string versionSelectorName,
        string mapperName,
        string factoryMethodName,
        string transferName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var eventTypeName = eventType.ToDisplayString(TypeFormat);
        var keyTypeName = keyType.ToDisplayString(TypeFormat);
        var stateTypeName = stateType.ToDisplayString(TypeFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.EnterpriseIntegration.EventCarriedStateTransfer.EventCarriedStateTransfer<")
            .Append(eventTypeName).Append(", ").Append(keyTypeName).Append(", ").Append(stateTypeName).Append("> ")
            .Append(factoryMethodName).AppendLine("()");
        sb.AppendLine(memberIndent + "{");
        sb.Append(bodyIndent).Append("return global::PatternKit.EnterpriseIntegration.EventCarriedStateTransfer.EventCarriedStateTransfer<")
            .Append(eventTypeName).Append(", ").Append(keyTypeName).Append(", ").Append(stateTypeName)
            .Append(">.Create(\"").Append(Escape(transferName)).AppendLine("\")");
        sb.Append(bodyIndent).Append("    .WithKey(").Append(keySelectorName).AppendLine(")");
        sb.Append(bodyIndent).Append("    .WithVersion(").Append(versionSelectorName).AppendLine(")");
        sb.Append(bodyIndent).Append("    .WithState(").Append(mapperName).AppendLine(")");
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

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

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
}
