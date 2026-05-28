using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.DomainServices;

[Generator]
public sealed class DomainServiceRegistryGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.DomainServices.GenerateDomainServiceRegistryAttribute";
    private const string OperationAttributeName = "PatternKit.Generators.DomainServices.DomainServiceOperationAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKDOM001",
        "Domain service registry host must be partial",
        "Type '{0}' is marked with [GenerateDomainServiceRegistry] but is not declared as partial",
        "PatternKit.Generators.DomainServices",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingOperations = new(
        "PKDOM002",
        "Domain service registry has no operations",
        "Type '{0}' is marked with [GenerateDomainServiceRegistry] but does not declare any domain service operations",
        "PatternKit.Generators.DomainServices",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidOperation = new(
        "PKDOM003",
        "Domain service operation signature is invalid",
        "Operation method '{0}' must be static, return TResponse, and accept exactly one TRequest parameter",
        "PatternKit.Generators.DomainServices",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateOperation = new(
        "PKDOM004",
        "Domain service operation is duplicated",
        "Domain service operation '{0}' is registered more than once",
        "PatternKit.Generators.DomainServices",
        DiagnosticSeverity.Error,
        true);

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

        var requestType = attribute.ConstructorArguments[0].Value as INamedTypeSymbol;
        var responseType = attribute.ConstructorArguments[1].Value as INamedTypeSymbol;
        if (requestType is null || responseType is null)
            return;

        var operations = GetOperations(type, requestType, responseType, context, out var hasAnnotatedOperations);
        if (!hasAnnotatedOperations)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingOperations, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (operations.Length == 0)
            return;

        if (TryFindDuplicate(operations, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateOperation, duplicate.Location, duplicate.Name));
            return;
        }

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        context.AddSource($"{type.Name}.DomainServiceRegistry.g.cs", SourceText.From(
            GenerateSource(type, requestType, responseType, operations, factoryMethodName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Operation> GetOperations(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        SourceProductionContext context,
        out bool hasAnnotatedOperations)
    {
        hasAnnotatedOperations = false;
        var builder = ImmutableArray.CreateBuilder<Operation>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attr in method.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != OperationAttributeName)
                    continue;

                hasAnnotatedOperations = true;
                if (!TryGetOperation(method, attr, requestType, responseType, out var operation))
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidOperation, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                builder.Add(operation);
            }
        }

        return builder.ToImmutable();
    }

    private static bool TryGetOperation(
        IMethodSymbol method,
        AttributeData attribute,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        out Operation operation)
    {
        operation = default;
        var name = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (!method.IsStatic || method.IsGenericMethod || method.Parameters.Length != 1)
            return false;

        if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, requestType)
            || !SymbolEqualityComparer.Default.Equals(method.ReturnType, responseType))
            return false;

        operation = new Operation(name!, method.Name, method.Locations.FirstOrDefault());
        return true;
    }

    private static bool TryFindDuplicate(IReadOnlyList<Operation> operations, out Operation duplicate)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if (!seen.Add(operation.Name))
            {
                duplicate = operation;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol requestType,
        INamedTypeSymbol responseType,
        IReadOnlyList<Operation> operations,
        string factoryMethodName)
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
        sb.Append("    public static global::PatternKit.Application.DomainServices.DomainServiceRegistry<")
            .Append(requestType.ToDisplayString(TypeFormat)).Append(", ")
            .Append(responseType.ToDisplayString(TypeFormat)).Append("> ")
            .Append(factoryMethodName).AppendLine("()");
        sb.AppendLine("    {");
        sb.Append("        var builder = global::PatternKit.Application.DomainServices.DomainServiceRegistry<")
            .Append(requestType.ToDisplayString(TypeFormat)).Append(", ")
            .Append(responseType.ToDisplayString(TypeFormat)).AppendLine(">.Create();");

        foreach (var operation in operations.OrderBy(static operation => operation.Name, System.StringComparer.Ordinal))
        {
            sb.Append("        builder.Add(\"")
                .Append(Escape(operation.Name))
                .Append("\", static request => ")
                .Append(operation.MethodName)
                .AppendLine("(request));");
        }

        sb.AppendLine("        return builder.Build();");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private readonly record struct Operation(string Name, string MethodName, Location? Location);
}
