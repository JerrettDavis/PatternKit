using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class BackplaneTopologyGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKBT001",
        "Backplane topology type must be partial",
        "Type '{0}' is marked with [GenerateBackplaneTopology] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingTopology = new(
        "PKBT002",
        "Backplane topology is empty",
        "Type '{0}' is marked with [GenerateBackplaneTopology] but does not declare any request/reply routes or subscriptions",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRequestReply = new(
        "PKBT003",
        "Backplane request/reply declaration is invalid",
        "Request/reply declaration '{0}' must reference valid request/response types, endpoint, handler, and optional predicate",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidSubscription = new(
        "PKBT004",
        "Backplane subscription declaration is invalid",
        "Subscription declaration '{0}' must reference a valid event type, topic, endpoint, and handler",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRequestDefault = new(
        "PKBT005",
        "Backplane request default route is duplicated",
        "Request type '{0}' has multiple default request/reply routes in '{1}'",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateBackplaneTopologyAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateBackplaneTopologyAttribute");
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

        var servicesType = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (servicesType is null)
            return;

        var hasTopologyAttributes = HasTopologyAttributes(type);
        var requests = GetRequestReplies(type, servicesType, context);
        var subscriptions = GetSubscriptions(type, servicesType, context);
        if (requests.Length == 0 && subscriptions.Length == 0)
        {
            if (!hasTopologyAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingTopology, node.Identifier.GetLocation(), type.Name));

            return;
        }

        if (HasDuplicateDefaults(requests, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRequestDefault, duplicate.Location, duplicate.RequestTypeName, type.Name));
            return;
        }

        var configureMethodName = GetNamedString(attribute, "ConfigureMethodName") ?? "Configure";
        var hostBuilderTypeName = GetNamedType(attribute, "HostBuilderType")
            ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?? "global::PatternKit.Examples.Messaging.BackplaneHostBuilder";
        context.AddSource($"{type.Name}.BackplaneTopology.g.cs", SourceText.From(
            GenerateSource(type, servicesType, hostBuilderTypeName, requests, subscriptions, configureMethodName),
            Encoding.UTF8));
    }

    private static ImmutableArray<RequestReply> GetRequestReplies(
        INamedTypeSymbol type,
        INamedTypeSymbol servicesType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<RequestReply>();
        foreach (var attr in type.GetAttributes().Where(static attr =>
                     attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.BackplaneRequestReplyAttribute"))
        {
            if (!TryGetRequestReply(type, servicesType, attr, out var request))
            {
                var endpoint = attr.ConstructorArguments.Length > 2 ? attr.ConstructorArguments[2].Value as string : null;
                context.ReportDiagnostic(Diagnostic.Create(InvalidRequestReply, attr.ApplicationSyntaxReference?.GetSyntax().GetLocation(), endpoint ?? type.Name));
                continue;
            }

            builder.Add(request);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetRequestReply(
        INamedTypeSymbol type,
        INamedTypeSymbol servicesType,
        AttributeData attribute,
        out RequestReply request)
    {
        request = default;
        if (attribute.ConstructorArguments.Length != 4)
            return false;

        var requestType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        var responseType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
        var endpointName = attribute.ConstructorArguments[2].Value as string;
        var handlerName = attribute.ConstructorArguments[3].Value as string;
        if (requestType is null || responseType is null || string.IsNullOrWhiteSpace(endpointName) || string.IsNullOrWhiteSpace(handlerName))
            return false;

        var handler = servicesType.GetMembers(handlerName!).OfType<IMethodSymbol>().FirstOrDefault();
        if (handler is null || !IsRequestHandler(handler, requestType, responseType))
            return false;

        var predicateName = GetNamedString(attribute, "PredicateMethodName");
        if (!string.IsNullOrWhiteSpace(predicateName))
        {
            var predicate = type.GetMembers(predicateName!).OfType<IMethodSymbol>().FirstOrDefault();
            if (predicate is null || !IsRoutePredicate(predicate, requestType))
                return false;
        }

        request = new RequestReply(
            requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            endpointName!,
            handler.Name,
            string.IsNullOrWhiteSpace(predicateName) ? null : predicateName,
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
        return true;
    }

    private static ImmutableArray<Subscription> GetSubscriptions(
        INamedTypeSymbol type,
        INamedTypeSymbol servicesType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Subscription>();
        foreach (var attr in type.GetAttributes().Where(static attr =>
                     attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.BackplaneSubscriptionAttribute"))
        {
            if (!TryGetSubscription(servicesType, attr, out var subscription))
            {
                var endpoint = attr.ConstructorArguments.Length > 2 ? attr.ConstructorArguments[2].Value as string : null;
                context.ReportDiagnostic(Diagnostic.Create(InvalidSubscription, attr.ApplicationSyntaxReference?.GetSyntax().GetLocation(), endpoint ?? type.Name));
                continue;
            }

            builder.Add(subscription);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetSubscription(
        INamedTypeSymbol servicesType,
        AttributeData attribute,
        out Subscription subscription)
    {
        subscription = default;
        if (attribute.ConstructorArguments.Length != 4)
            return false;

        var eventType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        var topic = attribute.ConstructorArguments[1].Value as string;
        var endpointName = attribute.ConstructorArguments[2].Value as string;
        var handlerName = attribute.ConstructorArguments[3].Value as string;
        if (eventType is null || string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(endpointName) || string.IsNullOrWhiteSpace(handlerName))
            return false;

        var handler = servicesType.GetMembers(handlerName!).OfType<IMethodSymbol>().FirstOrDefault();
        if (handler is null || !IsEventHandler(handler, eventType))
            return false;

        subscription = new Subscription(
            eventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            topic!,
            endpointName!,
            handler.Name,
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
        return true;
    }

    private static bool IsRoutePredicate(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 2 &&
           IsMessageOf(method.Parameters[0].Type, requestType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsRequestHandler(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => !method.IsStatic &&
           IsValueTaskOf(method.ReturnType, responseType) &&
           method.Parameters.Length == 3 &&
           IsMessageOf(method.Parameters[0].Type, requestType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static bool IsEventHandler(IMethodSymbol method, INamedTypeSymbol eventType)
        => !method.IsStatic &&
           method.ReturnType.ToDisplayString() == "System.Threading.Tasks.ValueTask" &&
           method.Parameters.Length == 3 &&
           IsMessageOf(method.Parameters[0].Type, eventType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static bool IsValueTaskOf(ITypeSymbol type, INamedTypeSymbol resultType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], resultType);

    private static bool IsMessageOf(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool HasDuplicateDefaults(IReadOnlyList<RequestReply> requests, out RequestReply duplicate)
    {
        var defaults = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var request in requests)
        {
            if (request.PredicateMethodName is not null)
                continue;

            if (!defaults.Add(request.RequestTypeName))
            {
                duplicate = request;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol servicesType,
        string hostBuilderTypeName,
        IReadOnlyList<RequestReply> requests,
        IReadOnlyList<Subscription> subscriptions,
        string configureMethodName)
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
        sb.Append("    public static ").Append(hostBuilderTypeName).Append(' ')
            .Append(configureMethodName)
            .Append('(').Append(hostBuilderTypeName).Append(" builder, ")
            .Append(servicesType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine(" services)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (builder is null)");
        sb.AppendLine("            throw new global::System.ArgumentNullException(nameof(builder));");
        sb.AppendLine("        if (services is null)");
        sb.AppendLine("            throw new global::System.ArgumentNullException(nameof(services));");
        sb.AppendLine();

        foreach (var request in requests
                     .OrderBy(static r => r.PredicateMethodName is null ? 1 : 0)
                     .ThenBy(static r => r.EndpointName, System.StringComparer.Ordinal))
        {
            if (request.PredicateMethodName is null)
            {
                sb.Append("        builder.MapDefaultCommand<")
                    .Append(request.RequestTypeName).Append(", ").Append(request.ResponseTypeName)
                    .Append(">(\"").Append(Escape(request.EndpointName)).AppendLine("\");");
            }
            else
            {
                sb.Append("        builder.MapCommand<")
                    .Append(request.RequestTypeName).Append(", ").Append(request.ResponseTypeName)
                    .Append(">(").Append(request.PredicateMethodName)
                    .Append(", \"").Append(Escape(request.EndpointName)).AppendLine("\");");
            }
        }

        sb.AppendLine();
        foreach (var group in requests.GroupBy(static r => r.EndpointName).OrderBy(static g => g.Key, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.ReceiveEndpoint(\"").Append(Escape(group.Key)).AppendLine("\", endpoint =>");
            sb.AppendLine("        {");
            foreach (var request in group.OrderBy(static r => r.HandlerMethodName, System.StringComparer.Ordinal))
            {
                sb.Append("            endpoint.HandleCommand<")
                    .Append(request.RequestTypeName).Append(", ").Append(request.ResponseTypeName)
                    .Append(">(services.").Append(request.HandlerMethodName).AppendLine(");");
            }

            sb.AppendLine("        });");
        }

        foreach (var group in subscriptions.GroupBy(static s => s.EndpointName).OrderBy(static g => g.Key, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.ReceiveEndpoint(\"").Append(Escape(group.Key)).AppendLine("\", endpoint =>");
            sb.AppendLine("        {");
            foreach (var subscription in group.OrderBy(static s => s.Topic, System.StringComparer.Ordinal).ThenBy(static s => s.HandlerMethodName, System.StringComparer.Ordinal))
            {
                sb.Append("            endpoint.Subscribe<")
                    .Append(subscription.EventTypeName)
                    .Append(">(\"").Append(Escape(subscription.Topic))
                    .Append("\", services.").Append(subscription.HandlerMethodName).AppendLine(");");
            }

            sb.AppendLine("        });");
        }

        sb.AppendLine();
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static bool HasTopologyAttributes(INamedTypeSymbol type)
        => type.GetAttributes().Any(static attr =>
            attr.AttributeClass?.ToDisplayString() is
                "PatternKit.Generators.Messaging.BackplaneRequestReplyAttribute" or
                "PatternKit.Generators.Messaging.BackplaneSubscriptionAttribute");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static INamedTypeSymbol? GetNamedType(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as INamedTypeSymbol;

    private readonly record struct RequestReply(
        string RequestTypeName,
        string ResponseTypeName,
        string EndpointName,
        string HandlerMethodName,
        string? PredicateMethodName,
        Location? Location);

    private readonly record struct Subscription(
        string EventTypeName,
        string Topic,
        string EndpointName,
        string HandlerMethodName,
        Location? Location);
}
