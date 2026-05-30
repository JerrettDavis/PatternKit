using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.ServiceLayer;

[Generator]
public sealed class ServiceLayerOperationGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.ServiceLayer.GenerateServiceLayerOperationAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.ServiceLayer.ServiceLayerHandlerAttribute";
    private const string RuleAttributeName = "PatternKit.Generators.ServiceLayer.ServiceLayerRuleAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKSL001", "Service Layer host must be partial",
        "Type '{0}' is marked with [GenerateServiceLayerOperation] but is not declared as partial",
        "PatternKit.Generators.ServiceLayer", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingHandler = new(
        "PKSL002", "Service Layer handler is missing",
        "Service Layer operation '{0}' must declare exactly one [ServiceLayerHandler] method",
        "PatternKit.Generators.ServiceLayer", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKSL003", "Service Layer handler signature is invalid",
        "Service Layer handler '{0}' must be static and return ValueTask<TResponse> from TRequest and CancellationToken parameters",
        "PatternKit.Generators.ServiceLayer", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidRule = new(
        "PKSL004", "Service Layer rule signature is invalid",
        "Service Layer rule '{0}' must be static and return bool from one TRequest parameter",
        "PatternKit.Generators.ServiceLayer", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateRule = new(
        "PKSL005", "Service Layer rule order is duplicated",
        "Service Layer rule order values must be unique",
        "PatternKit.Generators.ServiceLayer", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == GenerateAttributeName);
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

        var requestType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var responseType = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (requestType is null || responseType is null)
            return;

        var handlers = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == HandlerAttributeName))
            .ToArray();
        if (handlers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandler, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var handler = handlers[0];
        if (!IsHandler(handler, requestType, responseType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, handler.Locations.FirstOrDefault(), handler.Name));
            return;
        }

        var rules = GetRules(type);
        if (rules.Select(static rule => rule.Order).Distinct().Count() != rules.Count)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateRule, node.Identifier.GetLocation()));
            return;
        }

        foreach (var rule in rules)
        {
            if (!IsRule(rule.Method, requestType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRule, rule.Method.Locations.FirstOrDefault(), rule.Method.Name));
                return;
            }
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var operationName = GetNamedString(attribute, "OperationName");
        if (string.IsNullOrWhiteSpace(operationName))
            operationName = type.Name;

        context.AddSource($"{type.Name}.ServiceLayer.g.cs", SourceText.From(
            GenerateSource(type, requestType, responseType, handler.Name, rules.OrderBy(static rule => rule.Order).ToArray(), factoryName, operationName!),
            Encoding.UTF8));
    }

    private static List<RuleConfig> GetRules(INamedTypeSymbol type)
        => type.GetMembers().OfType<IMethodSymbol>()
            .SelectMany(method => method.GetAttributes()
                .Where(static attr => attr.AttributeClass?.ToDisplayString() == RuleAttributeName)
                .Select(attr => new RuleConfig(
                    method,
                    attr.ConstructorArguments[0].Value?.ToString() ?? method.Name,
                    attr.ConstructorArguments[1].Value?.ToString() ?? method.Name,
                    (int)(attr.ConstructorArguments[2].Value ?? 0))))
            .ToList();

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        string handlerName,
        IReadOnlyList<RuleConfig> rules,
        string factoryName,
        string operationName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var requestName = requestType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var responseName = responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        var containingTypes = GetContainingTypes(type);
        var indentLevel = 0;
        foreach (var containingType in containingTypes)
        {
            AppendTypeDeclaration(sb, containingType, indentLevel);
            sb.AppendLine();
            sb.AppendLine(new string(' ', indentLevel * 4) + "{");
            indentLevel++;
        }

        AppendTypeDeclaration(sb, type, indentLevel);
        sb.AppendLine();
        var indent = new string(' ', indentLevel * 4);
        sb.AppendLine(indent + "{");
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.ServiceLayer.ServiceLayerOperation<")
            .Append(requestName).Append(", ").Append(responseName).Append("> ").Append(factoryName).AppendLine("()");
        sb.AppendLine(memberIndent + "{");
        sb.Append(bodyIndent).Append("var builder = global::PatternKit.Application.ServiceLayer.ServiceLayerOperation<")
            .Append(requestName).Append(", ").Append(responseName).Append(">.Create(\"").Append(Escape(operationName)).AppendLine("\");");
        foreach (var rule in rules)
            sb.Append(bodyIndent).Append("builder.Require(\"").Append(Escape(rule.Code)).Append("\", \"").Append(Escape(rule.Message)).Append("\", ").Append(rule.Method.Name).AppendLine(");");
        sb.Append(bodyIndent).Append("return builder.Handle(").Append(handlerName).AppendLine(").Build();");
        sb.AppendLine(memberIndent + "}");
        sb.AppendLine(indent + "}");
        for (var i = containingTypes.Length - 1; i >= 0; i--)
        {
            sb.AppendLine(new string(' ', i * 4) + "}");
        }

        return sb.ToString();
    }

    private static INamedTypeSymbol[] GetContainingTypes(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        return containingTypes.ToArray();
    }

    private static void AppendTypeDeclaration(StringBuilder sb, INamedTypeSymbol type, int indentLevel)
    {
        sb.Append(new string(' ', indentLevel * 4));
        sb.Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name);
    }

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol requestType, INamedTypeSymbol responseType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.ReturnType is INamedTypeSymbol returnType
        && returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask<" + responseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">"
        && method.Parameters.Length == 2
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType)
        && method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";

    private static bool IsRule(IMethodSymbol method, INamedTypeSymbol requestType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.ReturnType.SpecialType == SpecialType.System_Boolean
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType);

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

    private sealed record RuleConfig(IMethodSymbol Method, string Code, string Message, int Order);
}
