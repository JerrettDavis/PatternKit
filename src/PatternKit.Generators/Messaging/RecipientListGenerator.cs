using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class RecipientListGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKRL001",
        "Recipient list type must be partial",
        "Type '{0}' is marked with [GenerateRecipientList] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingRecipients = new(
        "PKRL002",
        "Recipient list has no recipients",
        "Type '{0}' is marked with [GenerateRecipientList] but does not declare any [RecipientListRecipient] methods",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRecipient = new(
        "PKRL003",
        "Recipient list recipient signature is invalid",
        "Recipient list handler '{0}' must be static and return void or ValueTask with the required message/context parameters and matching predicate",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRecipient = new(
        "PKRL004",
        "Recipient list recipient name or order is duplicated",
        "Recipient list recipient '{0}' duplicates another recipient name or order in '{1}'",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateRecipientListAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateRecipientListAttribute");
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

        var payloadType = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (payloadType is null)
            return;

        var hasRecipientAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.RecipientListRecipientAttribute"));
        var recipients = GetRecipients(type, payloadType, context);
        if (recipients.Length == 0)
        {
            if (!hasRecipientAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingRecipients, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (HasDuplicates(recipients, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRecipient, duplicate.Location, duplicate.Name, type.Name));
            return;
        }

        var syncRecipients = recipients.Where(static recipient => !recipient.IsAsync).OrderBy(static recipient => recipient.Order).ThenBy(static recipient => recipient.Name).ToArray();
        var asyncRecipients = recipients.Where(static recipient => recipient.IsAsync).OrderBy(static recipient => recipient.Order).ThenBy(static recipient => recipient.Name).ToArray();
        var config = new RecipientListConfig(
            GetNamedString(attribute, "FactoryName") ?? "Create",
            GetNamedString(attribute, "AsyncFactoryName") ?? "CreateAsync");

        context.AddSource($"{type.Name}.RecipientList.g.cs", SourceText.From(GenerateSource(type, payloadType, syncRecipients, asyncRecipients, config), Encoding.UTF8));
    }

    private static ImmutableArray<Recipient> GetRecipients(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Recipient>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.RecipientListRecipientAttribute");
            if (attr is null)
                continue;

            if (!TryGetRecipient(type, method, payloadType, attr, out var recipient))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRecipient, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(recipient);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetRecipient(
        INamedTypeSymbol type,
        IMethodSymbol handler,
        INamedTypeSymbol payloadType,
        AttributeData attribute,
        out Recipient recipient)
    {
        recipient = default;
        if (!handler.IsStatic || attribute.ConstructorArguments.Length != 3)
            return false;

        var name = attribute.ConstructorArguments[0].Value as string;
        var order = attribute.ConstructorArguments[1].Value as int? ?? 0;
        var predicateName = attribute.ConstructorArguments[2].Value as string;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(predicateName))
            return false;

        var predicate = type.GetMembers(predicateName!).OfType<IMethodSymbol>().FirstOrDefault();
        if (predicate is null)
            return false;

        if (IsSyncHandler(handler, payloadType) && IsSyncPredicate(predicate, payloadType))
        {
            recipient = new Recipient(name!, order, predicate.Name, handler.Name, false, handler.Locations.FirstOrDefault());
            return true;
        }

        if (IsAsyncHandler(handler, payloadType) && IsAsyncPredicate(predicate, payloadType))
        {
            recipient = new Recipient(name!, order, predicate.Name, handler.Name, true, handler.Locations.FirstOrDefault());
            return true;
        }

        return false;
    }

    private static bool IsSyncPredicate(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 2 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsSyncHandler(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 2 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsAsyncPredicate(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           IsValueTaskOfBoolean(method.ReturnType) &&
           method.Parameters.Length == 3 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static bool IsAsyncHandler(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.ToDisplayString() == "System.Threading.Tasks.ValueTask" &&
           method.Parameters.Length == 3 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static bool IsMessageOfPayload(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool IsValueTaskOfBoolean(ITypeSymbol type)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>" &&
           named.TypeArguments.Length == 1 &&
           named.TypeArguments[0].SpecialType == SpecialType.System_Boolean;

    private static bool HasDuplicates(IReadOnlyList<Recipient> recipients, out Recipient duplicate)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        var orders = new HashSet<int>();
        foreach (var recipient in recipients)
        {
            if (!names.Add(recipient.Name) || !orders.Add(recipient.Order))
            {
                duplicate = recipient;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        IReadOnlyList<Recipient> syncRecipients,
        IReadOnlyList<Recipient> asyncRecipients,
        RecipientListConfig config)
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
        if (syncRecipients.Count > 0)
        {
            sb.Append("    public static global::PatternKit.Messaging.Routing.RecipientList<")
                .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("> ")
                .Append(config.FactoryName)
                .AppendLine("()");
            sb.AppendLine("        => global::PatternKit.Messaging.Routing.RecipientList<" + payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">.Create()");
            foreach (var recipient in syncRecipients)
                sb.Append("            .When(\"").Append(Escape(recipient.Name)).Append("\", ").Append(recipient.PredicateMethodName).Append(").Then(").Append(recipient.HandlerMethodName).AppendLine(")");
            sb.AppendLine("            .Build();");
            sb.AppendLine();
        }

        if (asyncRecipients.Count > 0)
        {
            sb.Append("    public static global::PatternKit.Messaging.Routing.AsyncRecipientList<")
                .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append("> ")
                .Append(config.AsyncFactoryName)
                .AppendLine("()");
            sb.AppendLine("        => global::PatternKit.Messaging.Routing.AsyncRecipientList<" + payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">.Create()");
            foreach (var recipient in asyncRecipients)
                sb.Append("            .When(\"").Append(Escape(recipient.Name)).Append("\", ").Append(recipient.PredicateMethodName).Append(").Then(").Append(recipient.HandlerMethodName).AppendLine(")");
            sb.AppendLine("            .Build();");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private readonly record struct Recipient(
        string Name,
        int Order,
        string PredicateMethodName,
        string HandlerMethodName,
        bool IsAsync,
        Location? Location);

    private readonly record struct RecipientListConfig(string FactoryName, string AsyncFactoryName);
}
