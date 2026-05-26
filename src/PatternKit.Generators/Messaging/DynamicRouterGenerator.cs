using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class DynamicRouterGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKDR001",
        "Dynamic router type must be partial",
        "Type '{0}' is marked with [GenerateDynamicRouter] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingRoutes = new(
        "PKDR002",
        "Dynamic router has no routes",
        "Type '{0}' is marked with [GenerateDynamicRouter] but does not declare any [DynamicRoute] methods",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidRoute = new(
        "PKDR003",
        "Dynamic router route signature is invalid",
        "Dynamic route '{0}' must be static, return TResult, and reference a static bool predicate with Message<TPayload> and MessageContext parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidDefault = new(
        "PKDR004",
        "Dynamic router default signature is invalid",
        "Dynamic router default '{0}' must be static and return TResult with Message<TPayload> and MessageContext parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateRoute = new(
        "PKDR005",
        "Dynamic router route name or order is duplicated",
        "Dynamic route '{0}' duplicates another route name or order in '{1}'",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateDynamicRouterAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateDynamicRouterAttribute");
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

        var payloadType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var resultType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (payloadType is null || resultType is null)
            return;

        var hasRouteAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.DynamicRouteAttribute"));
        var routes = GetRoutes(type, payloadType, resultType, context);
        if (routes.Length == 0)
        {
            if (!hasRouteAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingRoutes, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (HasDuplicates(routes, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRoute, duplicate.Location, duplicate.Name, type.Name));
            return;
        }

        var defaultHandler = GetDefaultHandler(type, payloadType, resultType, context);
        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var ordered = routes.OrderBy(static route => route.Order).ThenBy(static route => route.Name).ToArray();

        context.AddSource($"{type.Name}.DynamicRouter.g.cs", SourceText.From(
            GenerateSource(type, payloadType, resultType, ordered, defaultHandler, factoryName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Route> GetRoutes(INamedTypeSymbol type, INamedTypeSymbol payloadType, INamedTypeSymbol resultType, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Route>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.DynamicRouteAttribute");
            if (attr is null)
                continue;

            if (!TryGetRoute(type, method, payloadType, resultType, attr, out var route))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRoute, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(route);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetRoute(INamedTypeSymbol type, IMethodSymbol handler, INamedTypeSymbol payloadType, INamedTypeSymbol resultType, AttributeData attribute, out Route route)
    {
        route = default;
        if (!handler.IsStatic || attribute.ConstructorArguments.Length != 3)
            return false;

        var name = attribute.ConstructorArguments[0].Value as string;
        var order = attribute.ConstructorArguments[1].Value as int? ?? 0;
        var predicateName = attribute.ConstructorArguments[2].Value as string;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(predicateName))
            return false;

        var predicate = type.GetMembers(predicateName!).OfType<IMethodSymbol>().FirstOrDefault();
        if (predicate is null || !IsPredicate(predicate, payloadType) || !IsHandler(handler, payloadType, resultType))
            return false;

        route = new Route(name!, order, predicate.Name, handler.Name, handler.Locations.FirstOrDefault());
        return true;
    }

    private static string? GetDefaultHandler(INamedTypeSymbol type, INamedTypeSymbol payloadType, INamedTypeSymbol resultType, SourceProductionContext context)
    {
        string? handler = null;
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (!method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.DynamicRouteDefaultAttribute"))
                continue;

            if (handler is not null || !IsHandler(method, payloadType, resultType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidDefault, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            handler = method.Name;
        }

        return handler;
    }

    private static bool IsPredicate(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 2 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol payloadType, INamedTypeSymbol resultType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, resultType) &&
           method.Parameters.Length == 2 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsMessageOfPayload(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool HasDuplicates(IReadOnlyList<Route> routes, out Route duplicate)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        var orders = new HashSet<int>();
        foreach (var route in routes)
        {
            if (!names.Add(route.Name) || !orders.Add(route.Order))
            {
                duplicate = route;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(INamedTypeSymbol type, INamedTypeSymbol payloadType, INamedTypeSymbol resultType, IReadOnlyList<Route> routes, string? defaultHandler, string factoryName)
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
        sb.Append("    public static global::PatternKit.Messaging.Routing.DynamicRouter<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(", ")
            .Append(resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Routing.DynamicRouter<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(", ")
            .Append(resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine(">.Create()");

        foreach (var route in routes)
            sb.Append("            .When(@\"").Append(route.Name).Append("\", ").Append(route.Order).Append(", ").Append(route.PredicateMethodName).Append(").Then(").Append(route.HandlerMethodName).AppendLine(")");

        if (defaultHandler is not null)
            sb.Append("            .Default(").Append(defaultHandler).AppendLine(")");

        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private readonly record struct Route(string Name, int Order, string PredicateMethodName, string HandlerMethodName, Location? Location);
}
