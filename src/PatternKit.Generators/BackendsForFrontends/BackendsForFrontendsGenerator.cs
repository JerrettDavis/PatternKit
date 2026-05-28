using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.BackendsForFrontends;

[Generator]
public sealed class BackendsForFrontendsGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.BackendsForFrontends.GenerateBackendsForFrontendsAttribute";
    private const string SelectorAttributeName = "PatternKit.Generators.BackendsForFrontends.FrontendSelectorAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.BackendsForFrontends.FrontendHandlerAttribute";
    private const string FallbackAttributeName = "PatternKit.Generators.BackendsForFrontends.FrontendFallbackAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKBFF001", "Backends for Frontends host must be partial",
        "Type '{0}' is marked with [GenerateBackendsForFrontends] but is not declared as partial",
        "PatternKit.Generators.BackendsForFrontends", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKBFF002", "Backends for Frontends members are missing",
        "Backends for Frontends type '{0}' must declare matching frontend selectors and handlers",
        "PatternKit.Generators.BackendsForFrontends", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKBFF003", "Backends for Frontends method signature is invalid",
        "Backends for Frontends method '{0}' has an invalid static signature for the configured request or response type",
        "PatternKit.Generators.BackendsForFrontends", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateFrontend = new(
        "PKBFF004", "Backends for Frontends frontend is duplicated",
        "Frontend '{0}' is duplicated",
        "PatternKit.Generators.BackendsForFrontends", DiagnosticSeverity.Error, true);

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

        var selectors = NamedMembers(type, SelectorAttributeName);
        var handlers = NamedMembers(type, HandlerAttributeName);
        var fallbacks = MembersWith(type, FallbackAttributeName);
        if (selectors.Length == 0 || selectors.Length != handlers.Length || fallbacks.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var allNames = selectors.Select(static item => item.Name).Concat(handlers.Select(static item => item.Name)).ToArray();
        var duplicate = allNames.GroupBy(static item => item, StringComparer.OrdinalIgnoreCase).FirstOrDefault(static group => group.Count() > 2);
        if (duplicate is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateFrontend, node.Identifier.GetLocation(), duplicate.Key));
            return;
        }

        var routes = selectors.Select(selector => new FrontendRoute(selector.Name, selector.Method, handlers.FirstOrDefault(handler => string.Equals(handler.Name, selector.Name, StringComparison.OrdinalIgnoreCase))?.Method)).ToArray();
        if (routes.Any(static route => route.Handler is null))
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var invalidSelector = selectors.FirstOrDefault(item => !IsSelector(item.Method, requestType));
        if (invalidSelector is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidSelector.Method.Locations.FirstOrDefault(), invalidSelector.Method.Name));
            return;
        }

        var invalidHandler = handlers.FirstOrDefault(item => !IsHandler(item.Method, requestType, responseType));
        if (invalidHandler is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, invalidHandler.Method.Locations.FirstOrDefault(), invalidHandler.Method.Name));
            return;
        }

        if (fallbacks.Length == 1 && !IsHandler(fallbacks[0], requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, fallbacks[0].Locations.FirstOrDefault(), fallbacks[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.BackendsForFrontends.g.cs", SourceText.From(GenerateSource(
            type,
            requestType,
            responseType,
            routes,
            fallbacks.FirstOrDefault()?.Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "GatewayName") ?? "backends-for-frontends"), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static NamedMember[] NamedMembers(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == attributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(static item => new NamedMember((string)item.Attribute!.ConstructorArguments[0].Value!, item.Method))
            .ToArray();

    private static bool IsSelector(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType);

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, responseType) &&
           method.Parameters.Length == 1 &&
           method.Parameters[0].Type is INamedTypeSymbol contextType &&
           contextType.ConstructedFrom.ToDisplayString() == "PatternKit.Cloud.BackendsForFrontends.BackendsForFrontendsContext<TRequest>" &&
           contextType.TypeArguments.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(contextType.TypeArguments[0], requestType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        IReadOnlyList<FrontendRoute> routes,
        string? fallbackName,
        string factoryMethodName,
        string gatewayName)
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
        sb.Append("    public static global::PatternKit.Cloud.BackendsForFrontends.BackendsForFrontends<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.Cloud.BackendsForFrontends.BackendsForFrontends<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append(">.Create(\"").Append(Escape(gatewayName)).AppendLine("\")");
        foreach (var route in routes)
            sb.Append("            .Frontend(\"").Append(Escape(route.Name)).Append("\", ").Append(route.Selector.Name).Append(", ").Append(route.Handler!.Name).AppendLine(")");
        if (fallbackName is not null)
            sb.Append("            .Fallback(").Append(fallbackName).AppendLine(")");
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

    private sealed record NamedMember(string Name, IMethodSymbol Method);

    private sealed record FrontendRoute(string Name, IMethodSymbol Selector, IMethodSymbol? Handler);
}
