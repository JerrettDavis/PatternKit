using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class ScatterGatherGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new("PKSCG001", "Scatter-gather type must be partial", "Type '{0}' is marked with [GenerateScatterGather] but is not declared as partial", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingRecipients = new("PKSCG002", "Scatter-gather has no recipients", "Type '{0}' is marked with [GenerateScatterGather] but does not declare any [ScatterGatherRecipient] methods", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidRecipient = new("PKSCG003", "Scatter-gather recipient signature is invalid", "Recipient '{0}' must be static and return ScatterGatherReply<TResponse> with Message<TRequest> and MessageContext parameters", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidAggregator = new("PKSCG004", "Scatter-gather aggregator signature is invalid", "Aggregator '{0}' must be static and return TResult with IReadOnlyList<ScatterGatherReply<TResponse>>, Message<TRequest>, and MessageContext parameters", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateRecipient = new("PKSCG005", "Scatter-gather recipient name or order is duplicated", "Recipient '{0}' duplicates another recipient name or order in '{1}'", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateScatterGatherAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateScatterGatherAttribute");
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

        var requestType = GetTypeArgument(attribute, 0);
        var responseType = GetTypeArgument(attribute, 1);
        var resultType = GetTypeArgument(attribute, 2);
        if (requestType is null || responseType is null || resultType is null)
            return;

        var hasRecipientAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.ScatterGatherRecipientAttribute"));
        var recipients = GetRecipients(type, requestType, responseType, context);
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

        var aggregator = type.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.ScatterGatherAggregatorAttribute"));
        if (aggregator is null || !IsAggregator(aggregator, requestType, responseType, resultType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidAggregator, (aggregator is null ? type.Locations : aggregator.Locations).FirstOrDefault(), aggregator?.Name ?? type.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var name = GetNamedString(attribute, "Name") ?? "scatter-gather";
        context.AddSource($"{type.Name}.ScatterGather.g.cs", SourceText.From(GenerateSource(type, requestType, responseType, resultType, recipients.OrderBy(static r => r.Order).ThenBy(static r => r.Name).ToArray(), aggregator.Name, factoryName, name), Encoding.UTF8));
    }

    private static ImmutableArray<Recipient> GetRecipients(INamedTypeSymbol type, INamedTypeSymbol requestType, INamedTypeSymbol responseType, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Recipient>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.ScatterGatherRecipientAttribute");
            if (attr is null)
                continue;

            if (!IsRecipient(method, requestType, responseType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRecipient, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            var name = attr.ConstructorArguments[0].Value as string;
            var order = attr.ConstructorArguments.Length >= 2 ? attr.ConstructorArguments[1].Value as int? ?? 0 : 0;
            var predicate = attr.ConstructorArguments.Length >= 3 ? attr.ConstructorArguments[2].Value as string : null;
            if (string.IsNullOrWhiteSpace(name) || (!string.IsNullOrWhiteSpace(predicate) && !HasPredicate(type, predicate!, requestType)))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRecipient, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(new(name!, order, method.Name, string.IsNullOrWhiteSpace(predicate) ? null : predicate, method.Locations.FirstOrDefault()));
        }

        return builder.ToImmutable();
    }

    private static bool IsRecipient(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic &&
           method.Parameters.Length == 2 &&
           IsMessageOf(method.Parameters[0].Type, requestType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           IsScatterGatherReplyOf(method.ReturnType, responseType);

    private static bool IsAggregator(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType, INamedTypeSymbol resultType)
        => method.IsStatic &&
           method.Parameters.Length == 3 &&
           IsReplyListOf(method.Parameters[0].Type, responseType) &&
           IsMessageOf(method.Parameters[1].Type, requestType) &&
           method.Parameters[2].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, resultType);

    private static bool HasPredicate(INamedTypeSymbol type, string name, INamedTypeSymbol requestType)
        => type.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean &&
            method.Parameters.Length == 2 &&
            IsMessageOf(method.Parameters[0].Type, requestType) &&
            method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext");

    private static bool IsMessageOf(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named && named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool IsScatterGatherReplyOf(ITypeSymbol type, INamedTypeSymbol responseType)
        => type is INamedTypeSymbol named && named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Routing.ScatterGatherReply<TResponse>" && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], responseType);

    private static bool IsReplyListOf(ITypeSymbol type, INamedTypeSymbol responseType)
        => type is INamedTypeSymbol named &&
           named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyList<T>" &&
           IsScatterGatherReplyOf(named.TypeArguments[0], responseType);

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

    private static string GenerateSource(INamedTypeSymbol type, INamedTypeSymbol requestType, INamedTypeSymbol responseType, INamedTypeSymbol resultType, IReadOnlyList<Recipient> recipients, string aggregator, string factoryName, string name)
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
        sb.Append("    public static global::PatternKit.Messaging.Routing.ScatterGather<")
            .Append(requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(", ")
            .Append(responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(", ")
            .Append(resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("> ")
            .Append(factoryName).AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Routing.ScatterGather<")
            .Append(requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(", ")
            .Append(responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(", ")
            .Append(resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">.Create(").Append(ToLiteral(name)).AppendLine(")");
        foreach (var recipient in recipients)
        {
            sb.Append("            .AddRecipient(").Append(ToLiteral(recipient.Name)).Append(", ").Append(recipient.MethodName);
            if (recipient.PredicateMethodName is not null)
                sb.Append(", ").Append(recipient.PredicateMethodName);
            sb.AppendLine(")");
        }
        sb.Append("            .AggregateWith(").Append(aggregator).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static INamedTypeSymbol? GetTypeArgument(AttributeData attribute, int index)
        => attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as INamedTypeSymbol : null;

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string ToLiteral(string value) => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private readonly record struct Recipient(string Name, int Order, string MethodName, string? PredicateMethodName, Location? Location);
}
