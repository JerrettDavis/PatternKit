using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.GatewayRouting;

[Generator]
public sealed class GatewayRoutingGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.GatewayRouting.GenerateGatewayRoutingAttribute";
    private const string RouteAttributeName = "PatternKit.Generators.GatewayRouting.GatewayRouteAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.GatewayRouting.GatewayRouteHandlerAttribute";
    private const string FallbackAttributeName = "PatternKit.Generators.GatewayRouting.GatewayRouteFallbackAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKGR001", "Gateway Routing host must be partial",
        "Type '{0}' is marked with [GenerateGatewayRouting] but is not declared as partial",
        "PatternKit.Generators.GatewayRouting", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKGR002", "Gateway Routing members are missing",
        "Gateway Routing type '{0}' must declare at least one route predicate, matching route handlers, and exactly one fallback handler",
        "PatternKit.Generators.GatewayRouting", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKGR003", "Gateway Routing method signature is invalid",
        "Gateway Routing method '{0}' has an invalid static signature for the configured request or response type",
        "PatternKit.Generators.GatewayRouting", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateRoute = new(
        "PKGR004", "Gateway Routing route is duplicated",
        "Gateway Routing route name '{0}' is duplicated",
        "PatternKit.Generators.GatewayRouting", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingHandler = new(
        "PKGR005", "Gateway Routing handler is missing",
        "Gateway Routing route '{0}' must have exactly one matching handler",
        "PatternKit.Generators.GatewayRouting", DiagnosticSeverity.Error, true);

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

        var requestType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var responseType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (requestType is null || responseType is null)
            return;

        var routes = NamedMembers(type, RouteAttributeName);
        var handlers = NamedMembers(type, HandlerAttributeName);
        var fallback = MembersWith(type, FallbackAttributeName);
        if (routes.Length == 0 || fallback.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var duplicate = routes.Concat(handlers).GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(static group => group.Count(item => item.Kind == MemberKind.Route) > 1 || group.Count(item => item.Kind == MemberKind.Handler) > 1);
        if (duplicate is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRoute, node.Identifier.GetLocation(), duplicate.Key));
            return;
        }

        var invalidRoute = routes.FirstOrDefault(item => !IsRoute(item.Method, requestType));
        if (invalidRoute is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidRoute.Method.Locations.FirstOrDefault(), invalidRoute.Method.Name));
            return;
        }

        var invalidHandler = handlers.FirstOrDefault(item => !IsHandler(item.Method, requestType, responseType));
        if (invalidHandler is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidHandler.Method.Locations.FirstOrDefault(), invalidHandler.Method.Name));
            return;
        }

        if (!IsHandler(fallback[0].Method, requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, fallback[0].Method.Locations.FirstOrDefault(), fallback[0].Method.Name));
            return;
        }

        var pairs = routes.Select(route => new RoutePair(route.Name, route.Method, handlers.SingleOrDefault(handler => string.Equals(handler.Name, route.Name, StringComparison.OrdinalIgnoreCase))?.Method)).ToArray();
        var missing = pairs.FirstOrDefault(pair => pair.Handler is null);
        if (missing is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandler, node.Identifier.GetLocation(), missing.Name));
            return;
        }

        context.AddSource($"{type.Name}.GatewayRouting.g.cs", SourceText.From(GenerateSource(
            type,
            requestType,
            responseType,
            pairs!,
            fallback[0],
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "GatewayName") ?? "gateway-routing"), Encoding.UTF8));
    }

    private static NamedMember[] NamedMembers(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == attributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(item => new NamedMember((string)item.Attribute!.ConstructorArguments[0].Value!, item.Method, attributeName == RouteAttributeName ? MemberKind.Route : MemberKind.Handler))
            .ToArray();

    private static FallbackMember[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == attributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(static item => new FallbackMember(item.Attribute!.ConstructorArguments.Length == 0 ? "fallback" : (string)item.Attribute.ConstructorArguments[0].Value!, item.Method))
            .ToArray();

    private static bool IsRoute(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType);

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, responseType) &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType);

    private static string GenerateSource(INamedTypeSymbol type, INamedTypeSymbol requestType, INamedTypeSymbol responseType, IReadOnlyList<RoutePair> pairs, FallbackMember fallback, string factoryMethodName, string gatewayName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var requestTypeName = requestType.ToDisplayString(TypeFormat);
        var responseTypeName = responseType.ToDisplayString(TypeFormat);
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
        sb.Append("    public static global::PatternKit.Cloud.GatewayRouting.GatewayRouting<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.Cloud.GatewayRouting.GatewayRouting<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append(">.Create(\"").Append(Escape(gatewayName)).AppendLine("\")");
        foreach (var pair in pairs)
            sb.Append("            .Route(\"").Append(Escape(pair.Name)).Append("\", ").Append(pair.Predicate.Name).Append(", ").Append(pair.Handler!.Name).AppendLine(")");
        sb.Append("            .Fallback(\"").Append(Escape(fallback.Name)).Append("\", ").Append(fallback.Method.Name).AppendLine(")");
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

    private enum MemberKind { Route, Handler }

    private sealed record NamedMember(string Name, IMethodSymbol Method, MemberKind Kind);

    private sealed record FallbackMember(string Name, IMethodSymbol Method);

    private sealed record RoutePair(string Name, IMethodSymbol Predicate, IMethodSymbol? Handler);
}
