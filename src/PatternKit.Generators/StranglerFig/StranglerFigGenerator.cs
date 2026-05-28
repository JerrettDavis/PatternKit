using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.StranglerFig;

[Generator]
public sealed class StranglerFigGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.StranglerFig.GenerateStranglerFigAttribute";
    private const string RouteAttributeName = "PatternKit.Generators.StranglerFig.StranglerFigRouteAttribute";
    private const string LegacyAttributeName = "PatternKit.Generators.StranglerFig.StranglerFigLegacyAttribute";
    private const string ModernAttributeName = "PatternKit.Generators.StranglerFig.StranglerFigModernAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKSF001", "Strangler Fig host must be partial",
        "Type '{0}' is marked with [GenerateStranglerFig] but is not declared as partial",
        "PatternKit.Generators.StranglerFig", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKSF002", "Strangler Fig members are missing",
        "Strangler Fig type '{0}' must declare at least one route, exactly one legacy handler, and exactly one modern handler",
        "PatternKit.Generators.StranglerFig", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKSF003", "Strangler Fig method signature is invalid",
        "Strangler Fig method '{0}' has an invalid static signature for the configured request or response type",
        "PatternKit.Generators.StranglerFig", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateRoute = new(
        "PKSF004", "Strangler Fig route is duplicated",
        "Strangler Fig route name '{0}' is duplicated",
        "PatternKit.Generators.StranglerFig", DiagnosticSeverity.Error, true);

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

        var routes = RouteMembers(type);
        var legacy = MembersWith(type, LegacyAttributeName);
        var modern = MembersWith(type, ModernAttributeName);
        if (routes.Length == 0 || legacy.Length != 1 || modern.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var duplicate = routes.GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(static group => group.Count() > 1);
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

        if (!IsHandler(legacy[0], requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, legacy[0].Locations.FirstOrDefault(), legacy[0].Name));
            return;
        }

        if (!IsHandler(modern[0], requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, modern[0].Locations.FirstOrDefault(), modern[0].Name));
            return;
        }

        context.AddSource($"{type.Name}.StranglerFig.g.cs", SourceText.From(GenerateSource(
            type,
            requestType,
            responseType,
            routes,
            legacy[0].Name,
            modern[0].Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "MigrationName") ?? "strangler-fig"), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static RouteMember[] RouteMembers(INamedTypeSymbol type)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Select(method => new
            {
                Method = method,
                Attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() == RouteAttributeName)
            })
            .Where(static item => item.Attribute is not null)
            .Select(static item => new RouteMember((string)item.Attribute!.ConstructorArguments[0].Value!, item.Method))
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

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        IReadOnlyList<RouteMember> routes,
        string legacyName,
        string modernName,
        string factoryMethodName,
        string migrationName)
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
        sb.Append("    public static global::PatternKit.Cloud.StranglerFig.StranglerFig<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.Cloud.StranglerFig.StranglerFig<")
            .Append(requestTypeName).Append(", ").Append(responseTypeName).Append(">.Create(\"").Append(Escape(migrationName)).AppendLine("\")");
        foreach (var route in routes)
            sb.Append("            .RouteToModern(\"").Append(Escape(route.Name)).Append("\", ").Append(route.Method.Name).AppendLine(")");
        sb.Append("            .Legacy(").Append(legacyName).AppendLine(")");
        sb.Append("            .Modern(").Append(modernName).AppendLine(")");
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

    private sealed record RouteMember(string Name, IMethodSymbol Method);
}
