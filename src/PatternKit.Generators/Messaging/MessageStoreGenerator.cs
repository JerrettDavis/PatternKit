using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class MessageStoreGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKMS001",
        "Message store type must be partial",
        "Type '{0}' is marked with [GenerateMessageStore] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidIdentity = new(
        "PKMS002",
        "Message store identity signature is invalid",
        "Message store identity method '{0}' must be static and return string with Message<TPayload> and MessageContext parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRetention = new(
        "PKMS003",
        "Message store retention signature is invalid",
        "Message store retention method '{0}' must be static and return bool with StoredMessage<TPayload> parameter",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateHook = new(
        "PKMS004",
        "Message store hook is duplicated",
        "Type '{0}' declares more than one [{1}] method",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateMessageStoreAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateMessageStoreAttribute");
            if (attr is null)
                return;

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

        var payloadType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (payloadType is null)
            return;

        var identities = type.GetMembers().OfType<IMethodSymbol>().Where(static method =>
            method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.MessageStoreIdentityAttribute")).ToArray();
        var retentions = type.GetMembers().OfType<IMethodSymbol>().Where(static method =>
            method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.MessageStoreRetentionAttribute")).ToArray();

        if (identities.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateHook, identities[1].Locations.FirstOrDefault(), type.Name, "MessageStoreIdentity"));
            return;
        }

        if (retentions.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateHook, retentions[1].Locations.FirstOrDefault(), type.Name, "MessageStoreRetention"));
            return;
        }

        var identity = identities.FirstOrDefault();
        if (identity is not null && !IsIdentity(identity, payloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidIdentity, identity.Locations.FirstOrDefault(), identity.Name));
            return;
        }

        var retention = retentions.FirstOrDefault();
        if (retention is not null && !IsRetention(retention, payloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidRetention, retention.Locations.FirstOrDefault(), retention.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var storeName = GetNamedString(attribute, "StoreName") ?? "message-store";
        context.AddSource($"{type.Name}.MessageStore.g.cs", SourceText.From(
            GenerateSource(type, payloadType, factoryName, storeName, identity?.Name, retention?.Name),
            Encoding.UTF8));
    }

    private static bool IsIdentity(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_String &&
           method.Parameters.Length == 2 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsRetention(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 1 &&
           IsStoredMessageOfPayload(method.Parameters[0].Type, payloadType);

    private static bool IsMessageOfPayload(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool IsStoredMessageOfPayload(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Storage.StoredMessage<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        string factoryName,
        string storeName,
        string? identityMethod,
        string? retentionMethod)
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
        sb.Append("    public static global::PatternKit.Messaging.Storage.MessageStore<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Storage.MessageStore<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">.Create(")
            .Append(ToLiteral(storeName))
            .AppendLine(")");

        if (identityMethod is not null)
            sb.Append("            .IdentifyBy(").Append(identityMethod).AppendLine(")");
        if (retentionMethod is not null)
            sb.Append("            .RetainWhen(").Append(retentionMethod).AppendLine(")");

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
}
