using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.HealthEndpointMonitoring;

[Generator]
public sealed class HealthEndpointMonitoringGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.HealthEndpointMonitoring.GenerateHealthEndpointAttribute";
    private const string CheckAttributeName = "PatternKit.Generators.HealthEndpointMonitoring.HealthEndpointCheckAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKHEM001", "Health Endpoint host must be partial",
        "Type '{0}' is marked with [GenerateHealthEndpoint] but is not declared as partial",
        "PatternKit.Generators.HealthEndpointMonitoring", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingChecks = new(
        "PKHEM002", "Health Endpoint checks are missing",
        "Health Endpoint type '{0}' must declare at least one [HealthEndpointCheck] method",
        "PatternKit.Generators.HealthEndpointMonitoring", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidCheck = new(
        "PKHEM003", "Health Endpoint check signature is invalid",
        "Health Endpoint check '{0}' must be static and return HealthEndpointCheckResult with one TContext parameter",
        "PatternKit.Generators.HealthEndpointMonitoring", DiagnosticSeverity.Error, true);

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

        var contextType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        if (contextType is null)
            return;

        var checks = type.GetMembers().OfType<IMethodSymbol>()
            .Select(static method => new CheckCandidate(
                method,
                method.GetAttributes().FirstOrDefault(static attr => attr.AttributeClass?.ToDisplayString() == CheckAttributeName)))
            .Where(static candidate => candidate.Attribute is not null)
            .ToArray();

        if (checks.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingChecks, node.Identifier.GetLocation(), type.Name));
            return;
        }

        foreach (var check in checks)
        {
            if (!IsCheck(check.Method, contextType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidCheck, check.Method.Locations.FirstOrDefault(), check.Method.Name));
                return;
            }
        }

        var configuredChecks = checks
            .Select(static check => new ConfiguredCheck(
                check.Method.Name,
                GetCheckName(check.Attribute!, check.Method.Name),
                GetNamedInt(check.Attribute!, "Order") ?? 0))
            .OrderBy(static check => check.Order)
            .ThenBy(static check => check.MethodName)
            .ToArray();

        context.AddSource($"{type.Name}.HealthEndpoint.g.cs", SourceText.From(GenerateSource(
            type,
            contextType,
            configuredChecks,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "EndpointName") ?? "health-endpoint"), Encoding.UTF8));
    }

    private static bool IsCheck(IMethodSymbol method, INamedTypeSymbol contextType)
        => method.IsStatic &&
           method.ReturnType.ToDisplayString() == "PatternKit.Cloud.HealthEndpointMonitoring.HealthEndpointCheckResult" &&
           method.Parameters.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol contextType,
        ConfiguredCheck[] checks,
        string factoryMethodName,
        string endpointName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var contextTypeName = contextType.ToDisplayString(TypeFormat);
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
        sb.Append("    public static global::PatternKit.Cloud.HealthEndpointMonitoring.HealthEndpoint<").Append(contextTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        return global::PatternKit.Cloud.HealthEndpointMonitoring.HealthEndpoint<").Append(contextTypeName).Append(">.Create(\"").Append(Escape(endpointName)).AppendLine("\")");
        foreach (var check in checks)
            sb.Append("            .WithCheck(\"").Append(Escape(check.Name)).Append("\", ").Append(check.MethodName).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetCheckName(AttributeData attribute, string fallback)
        => attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string name && !string.IsNullOrWhiteSpace(name)
            ? name
            : fallback;

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;

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

    private sealed record CheckCandidate(IMethodSymbol Method, AttributeData? Attribute);

    private sealed record ConfiguredCheck(string MethodName, string Name, int Order);
}
