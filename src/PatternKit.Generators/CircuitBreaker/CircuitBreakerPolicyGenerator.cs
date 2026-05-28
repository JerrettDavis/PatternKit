using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.CircuitBreaker;

[Generator]
public sealed class CircuitBreakerPolicyGenerator : IIncrementalGenerator
{
    private const string GenerateCircuitBreakerPolicyAttributeName = "PatternKit.Generators.CircuitBreaker.GenerateCircuitBreakerPolicyAttribute";
    private const string CircuitBreakerResultPredicateAttributeName = "PatternKit.Generators.CircuitBreaker.CircuitBreakerResultPredicateAttribute";
    private const string CircuitBreakerExceptionPredicateAttributeName = "PatternKit.Generators.CircuitBreaker.CircuitBreakerExceptionPredicateAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCB001",
        "Circuit breaker policy host must be partial",
        "Type '{0}' is marked with [GenerateCircuitBreakerPolicy] but is not declared as partial",
        "PatternKit.Generators.CircuitBreaker",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKCB002",
        "Circuit breaker policy configuration is invalid",
        "Circuit breaker policy '{0}' must have FailureThreshold >= 1 and BreakDurationMilliseconds >= 0",
        "PatternKit.Generators.CircuitBreaker",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidPredicate = new(
        "PKCB003",
        "Circuit breaker predicate signature is invalid",
        "Circuit breaker predicate method '{0}' must be static and return bool with a result or exception parameter",
        "PatternKit.Generators.CircuitBreaker",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MultiplePredicates = new(
        "PKCB004",
        "Circuit breaker predicate is duplicated",
        "Circuit breaker policy '{0}' has multiple {1} predicates",
        "PatternKit.Generators.CircuitBreaker",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateCircuitBreakerPolicyAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static a =>
                a.AttributeClass?.ToDisplayString() == GenerateCircuitBreakerPolicyAttributeName);
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

        var resultType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (resultType is null)
            return;

        var failureThreshold = GetNamedInt(attribute, "FailureThreshold") ?? 3;
        var breakDurationMilliseconds = GetNamedInt(attribute, "BreakDurationMilliseconds") ?? 30000;
        if (failureThreshold < 1 || breakDurationMilliseconds < 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var resultPredicates = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => HasAttribute(method, CircuitBreakerResultPredicateAttributeName))
            .ToArray();
        var exceptionPredicates = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => HasAttribute(method, CircuitBreakerExceptionPredicateAttributeName))
            .ToArray();

        if (resultPredicates.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MultiplePredicates, resultPredicates[1].Locations.FirstOrDefault(), type.Name, "result"));
            return;
        }

        if (exceptionPredicates.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MultiplePredicates, exceptionPredicates[1].Locations.FirstOrDefault(), type.Name, "exception"));
            return;
        }

        var resultPredicate = resultPredicates.FirstOrDefault();
        if (resultPredicate is not null && !IsResultPredicate(resultPredicate, resultType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidPredicate, resultPredicate.Locations.FirstOrDefault(), resultPredicate.Name));
            return;
        }

        var exceptionPredicate = exceptionPredicates.FirstOrDefault();
        if (exceptionPredicate is not null && !IsExceptionPredicate(exceptionPredicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidPredicate, exceptionPredicate.Locations.FirstOrDefault(), exceptionPredicate.Name));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        var policyName = GetNamedString(attribute, "PolicyName") ?? "circuit-breaker";
        context.AddSource($"{type.Name}.CircuitBreakerPolicy.g.cs", SourceText.From(
            GenerateSource(type, resultType, factoryMethodName, policyName, failureThreshold, breakDurationMilliseconds, resultPredicate, exceptionPredicate),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol resultType,
        string factoryMethodName,
        string policyName,
        int failureThreshold,
        int breakDurationMilliseconds,
        IMethodSymbol? resultPredicate,
        IMethodSymbol? exceptionPredicate)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var resultTypeName = resultType.ToDisplayString(TypeFormat);
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
        sb.Append("    public static global::PatternKit.Cloud.CircuitBreaker.CircuitBreakerPolicy<").Append(resultTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Cloud.CircuitBreaker.CircuitBreakerPolicy<").Append(resultTypeName).Append(">.Create(\"").Append(Escape(policyName)).AppendLine("\")");
        sb.Append("            .WithFailureThreshold(").Append(failureThreshold).AppendLine(")");
        sb.Append("            .WithBreakDuration(global::System.TimeSpan.FromMilliseconds(").Append(breakDurationMilliseconds).AppendLine("));");

        if (resultPredicate is not null)
            sb.Append("        builder.HandleResult(static result => ").Append(resultPredicate.Name).AppendLine("(result));");
        if (exceptionPredicate is not null)
            sb.Append("        builder.HandleException(static exception => ").Append(exceptionPredicate.Name).AppendLine("(exception));");

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsResultPredicate(IMethodSymbol method, ITypeSymbol resultType)
        => method.IsStatic
        && method.ReturnType.SpecialType == SpecialType.System_Boolean
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, resultType);

    private static bool IsExceptionPredicate(IMethodSymbol method)
        => method.IsStatic
        && method.ReturnType.SpecialType == SpecialType.System_Boolean
        && method.Parameters.Length == 1
        && InheritsFrom(method.Parameters[0].Type, "System.Exception");

    private static bool InheritsFrom(ITypeSymbol symbol, string metadataName)
    {
        for (var current = symbol as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == metadataName)
                return true;
        }

        return false;
    }

    private static bool HasAttribute(IMethodSymbol method, string metadataName)
        => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == metadataName);

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

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;
}
