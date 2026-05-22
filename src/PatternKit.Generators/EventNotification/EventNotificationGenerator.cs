using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.EventNotification;

[Generator]
public sealed class EventNotificationGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.EventNotification.GenerateEventNotificationAttribute";
    private const string KeyAttributeName = "PatternKit.Generators.EventNotification.EventNotificationKeyAttribute";
    private const string CorrelationAttributeName = "PatternKit.Generators.EventNotification.EventNotificationCorrelationAttribute";
    private const string RuleAttributeName = "PatternKit.Generators.EventNotification.EventNotificationRuleAttribute";
    private const string MetadataAttributeName = "PatternKit.Generators.EventNotification.EventNotificationMetadataAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKEN001", "Event Notification host must be partial",
        "Type '{0}' is marked with [GenerateEventNotification] but is not declared as partial",
        "PatternKit.Generators.EventNotification", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingKey = new(
        "PKEN002", "Event Notification key selector is missing",
        "Event Notification type '{0}' must declare exactly one [EventNotificationKey] method",
        "PatternKit.Generators.EventNotification", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKEN003", "Event Notification method signature is invalid",
        "Event Notification method '{0}' has an invalid static signature for the configured event or key type",
        "PatternKit.Generators.EventNotification", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateMetadata = new(
        "PKEN004", "Event Notification metadata is duplicated",
        "Event Notification metadata name '{0}' is duplicated",
        "PatternKit.Generators.EventNotification", DiagnosticSeverity.Error, true);

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
        if (eventType is null || keyType is null)
            return;

        var keys = MembersWith(type, KeyAttributeName);
        if (keys.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingKey, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var correlations = MembersWith(type, CorrelationAttributeName);
        var rules = MembersWith(type, RuleAttributeName);
        var metadata = MetadataMembers(type);
        var duplicate = metadata.GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateMetadata, node.Identifier.GetLocation(), duplicate.Key));
            return;
        }

        if (!IsSelector(keys[0], eventType, keyType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, keys[0].Locations.FirstOrDefault(), keys[0].Name));
            return;
        }

        var invalidCorrelation = correlations.FirstOrDefault(method => !IsStringSelector(method, eventType));
        var invalidRule = rules.FirstOrDefault(method => !IsBoolSelector(method, eventType));
        var invalidMetadata = metadata.FirstOrDefault(item => !IsStringSelector(item.Method, eventType));
        if (invalidCorrelation is not null || invalidRule is not null || invalidMetadata is not null)
        {
            var invalid = invalidCorrelation ?? invalidRule ?? invalidMetadata!.Method;
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalid.Locations.FirstOrDefault(), invalid.Name));
            return;
        }

        context.AddSource($"{type.Name}.EventNotification.g.cs", SourceText.From(GenerateSource(
            type,
            eventType,
            keyType,
            keys[0].Name,
            correlations.FirstOrDefault()?.Name,
            rules.FirstOrDefault()?.Name,
            metadata,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "NotificationName") ?? "event-notification"), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static MetadataMember[] MetadataMembers(INamedTypeSymbol type)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == MetadataAttributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(static item => new MetadataMember((string)item.Attribute!.ConstructorArguments[0].Value!, item.Method))
            .ToArray();

    private static bool IsSelector(IMethodSymbol method, INamedTypeSymbol eventType, ITypeSymbol returnType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, returnType) &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, eventType);

    private static bool IsStringSelector(IMethodSymbol method, INamedTypeSymbol eventType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_String &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, eventType);

    private static bool IsBoolSelector(IMethodSymbol method, INamedTypeSymbol eventType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, eventType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol eventType,
        INamedTypeSymbol keyType,
        string keySelectorName,
        string? correlationSelectorName,
        string? ruleName,
        IReadOnlyList<MetadataMember> metadata,
        string factoryMethodName,
        string notificationName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var eventTypeName = eventType.ToDisplayString(TypeFormat);
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

        sb.Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public static global::PatternKit.EnterpriseIntegration.EventNotification.EventNotification<")
            .Append(eventTypeName).Append(", ").Append(keyTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.EnterpriseIntegration.EventNotification.EventNotification<")
            .Append(eventTypeName).Append(", ").Append(keyTypeName).Append(">.Create(\"").Append(Escape(notificationName)).AppendLine("\")");
        if (!string.IsNullOrWhiteSpace(ruleName))
            sb.Append("            .When(").Append(ruleName).AppendLine(")");
        sb.Append("            .WithKey(").Append(keySelectorName).AppendLine(")");
        if (!string.IsNullOrWhiteSpace(correlationSelectorName))
            sb.Append("            .WithCorrelation(").Append(correlationSelectorName).AppendLine(")");
        foreach (var item in metadata)
            sb.Append("            .WithMetadata(\"").Append(Escape(item.Name)).Append("\", ").Append(item.Method.Name).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
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

    private sealed record MetadataMember(string Name, IMethodSymbol Method);
}
