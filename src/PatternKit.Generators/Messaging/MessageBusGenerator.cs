using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class MessageBusGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new("PKBUS001", "Message bus type must be partial", "Type '{0}' is marked with [GenerateMessageBus] but is not declared as partial", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingRoutes = new("PKBUS002", "Message bus has no routes", "Type '{0}' is marked with [GenerateMessageBus] but does not declare any [MessageBusRoute] methods", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidRoute = new("PKBUS003", "Message bus route signature is invalid", "Message bus route '{0}' must be static and return MessageChannel<TPayload>", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateMessageBusAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateMessageBusAttribute");
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

        var payloadType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        if (payloadType is null)
            return;

        var hasRouteAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.MessageBusRouteAttribute"));
        var routes = GetRoutes(type, payloadType, context);
        if (routes.Length == 0)
        {
            if (!hasRouteAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingRoutes, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var busName = GetNamedString(attribute, "BusName") ?? "message-bus";
        context.AddSource($"{type.Name}.MessageBus.g.cs", SourceText.From(GenerateSource(type, payloadType, routes, factoryName, busName), Encoding.UTF8));
    }

    private static ImmutableArray<Route> GetRoutes(INamedTypeSymbol type, INamedTypeSymbol payloadType, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Route>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attr in method.GetAttributes().Where(static a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.MessageBusRouteAttribute"))
            {
                if (!method.IsStatic || !ReturnsMessageChannel(method, payloadType) || attr.ConstructorArguments.Length != 1 || attr.ConstructorArguments[0].Value is not string topic || string.IsNullOrWhiteSpace(topic))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidRoute, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                builder.Add(new(topic, method.Name));
            }
        }

        return builder.ToImmutable();
    }

    private static bool ReturnsMessageChannel(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.Parameters.Length == 0 &&
           method.ReturnType is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Channels.MessageChannel<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static string GenerateSource(INamedTypeSymbol type, INamedTypeSymbol payloadType, ImmutableArray<Route> routes, string factoryName, string busName)
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
        sb.Append("    public static global::PatternKit.Messaging.Channels.MessageBus<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ").Append(factoryName).AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Channels.MessageBus<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">.Create(").Append(ToLiteral(busName)).AppendLine(")");

        foreach (var route in routes)
            sb.Append("            .Route(").Append(ToLiteral(route.Topic)).Append(", ").Append(route.MethodName).AppendLine("())");

        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string ToLiteral(string value) => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private readonly record struct Route(string Topic, string MethodName);
}
