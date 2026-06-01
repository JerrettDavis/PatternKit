using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.CompensatingTransactions;

[Generator]
public sealed class CompensatingTransactionGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.CompensatingTransactions.GenerateCompensatingTransactionAttribute";
    private const string StepAttributeName = "PatternKit.Generators.CompensatingTransactions.CompensatingTransactionStepAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKCOMP001", "Compensating transaction host must be partial",
        "Type '{0}' is marked with [GenerateCompensatingTransaction] but is not declared as partial",
        "PatternKit.Generators.CompensatingTransactions", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingSteps = new(
        "PKCOMP002", "Compensating transaction must declare steps",
        "Compensating transaction '{0}' must declare at least one [CompensatingTransactionStep] method",
        "PatternKit.Generators.CompensatingTransactions", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidStep = new(
        "PKCOMP003", "Compensating transaction step signature is invalid",
        "Compensating transaction step '{0}' must accept (TContext, CancellationToken) and return ValueTask",
        "PatternKit.Generators.CompensatingTransactions", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateStep = new(
        "PKCOMP004", "Compensating transaction step is duplicated",
        "Compensating transaction '{0}' has duplicate step names or orders",
        "PatternKit.Generators.CompensatingTransactions", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKCOMP005", "Compensating transaction configuration is invalid",
        "Compensating transaction '{0}' must have valid FactoryMethodName, TransactionName, and Compensation values",
        "PatternKit.Generators.CompensatingTransactions", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == GenerateAttributeName);
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

        var factoryMethodName = GetNamedString(attribute, "FactoryMethodName") ?? "Create";
        var transactionName = GetNamedString(attribute, "TransactionName") ?? "compensating-transaction";
        if (!IsValidIdentifier(factoryMethodName) || string.IsNullOrWhiteSpace(transactionName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var steps = GetSteps(type);
        if (steps.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingSteps, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var contextType = steps[0].Method.Parameters.Length >= 1 ? steps[0].Method.Parameters[0].Type : null;
        if (contextType is null || contextType.TypeKind == TypeKind.Error)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidStep, steps[0].Method.Locations.FirstOrDefault(), steps[0].Method.Name));
            return;
        }

        foreach (var step in steps)
        {
            if (!HasValidStepSignature(step.Method, contextType)
                || string.IsNullOrWhiteSpace(step.Compensation)
                || !HasValidStepSignature(FindMethod(type, step.Compensation), contextType)
                || (!string.IsNullOrWhiteSpace(step.Condition) && !HasValidCondition(type, step.Condition!, contextType)))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidStep, step.Method.Locations.FirstOrDefault(), step.Method.Name));
                return;
            }
        }

        if (steps.GroupBy(static step => step.Name).Any(static group => group.Count() > 1)
            || steps.GroupBy(static step => step.Order).Any(static group => group.Count() > 1))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateStep, node.Identifier.GetLocation(), type.Name));
            return;
        }

        context.AddSource($"{type.Name}.CompensatingTransaction.g.cs", SourceText.From(
            GenerateSource(type, contextType, factoryMethodName, transactionName, steps.OrderBy(static step => step.Order).ToArray()),
            Encoding.UTF8));
    }

    private static StepModel[] GetSteps(INamedTypeSymbol type)
        => type.GetMembers()
            .OfType<IMethodSymbol>()
            .Select(method => (Method: method, Attribute: method.GetAttributes().FirstOrDefault(static attribute => attribute.AttributeClass?.ToDisplayString() == StepAttributeName)))
            .Where(static item => item.Attribute is not null)
            .Select(static item => new StepModel(
                item.Method,
                item.Attribute!.ConstructorArguments[0].Value?.ToString() ?? item.Method.Name,
                (int)(item.Attribute.ConstructorArguments[1].Value ?? 0),
                GetNamedString(item.Attribute, "Compensation") ?? string.Empty,
                GetNamedString(item.Attribute, "Condition")))
            .ToArray();

    private static bool HasValidStepSignature(IMethodSymbol? method, ITypeSymbol? contextType)
        => method is not null
        && method.IsStatic
        && !method.IsGenericMethod
        && method.Parameters.Length == 2
        && contextType is not null
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType)
        && method.Parameters[1].Type.ToDisplayString() == "System.Threading.CancellationToken"
        && method.ReturnType.ToDisplayString() == "System.Threading.Tasks.ValueTask";

    private static bool HasValidCondition(INamedTypeSymbol type, string methodName, ITypeSymbol contextType)
    {
        var method = FindMethod(type, methodName);
        return method is not null
            && method.IsStatic
            && !method.IsGenericMethod
            && method.Parameters.Length == 1
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType)
            && method.ReturnType.SpecialType == SpecialType.System_Boolean;
    }

    private static IMethodSymbol? FindMethod(INamedTypeSymbol type, string methodName)
        => type.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();

    private static string GenerateSource(
        INamedTypeSymbol type,
        ITypeSymbol contextType,
        string factoryMethodName,
        string transactionName,
        IReadOnlyList<StepModel> steps)
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

        var indent = string.Empty;
        foreach (var containingType in GetContainingTypes(type))
        {
            AppendTypeDeclaration(sb, containingType, indent);
            sb.Append(indent).AppendLine("{");
            indent += "    ";
        }

        AppendTypeDeclaration(sb, type, indent);
        sb.Append(indent).AppendLine("{");
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.CompensatingTransactions.CompensatingTransaction<").Append(contextTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.Append(memberIndent).AppendLine("{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Application.CompensatingTransactions.CompensatingTransaction<").Append(contextTypeName).Append(">.Create(\"").Append(Escape(transactionName)).AppendLine("\")");
        foreach (var step in steps)
        {
            sb.Append(bodyIndent).Append("    .AddStep(\"").Append(Escape(step.Name)).Append("\", static (context, cancellationToken) => ").Append(step.Method.Name).Append("(context, cancellationToken), static (context, cancellationToken) => ").Append(step.Compensation).Append("(context, cancellationToken), step => step.At(").Append(step.Order).Append(')');
            if (!string.IsNullOrWhiteSpace(step.Condition))
                sb.Append(".When(static context => ").Append(step.Condition).Append("(context))");
            sb.AppendLine(")");
        }

        sb.Append(bodyIndent).AppendLine("    .Build();");
        sb.Append(memberIndent).AppendLine("}");
        sb.Append(indent).AppendLine("}");
        while (indent.Length > 0)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.Append(indent).AppendLine("}");
        }

        return sb.ToString();
    }

    private static IReadOnlyList<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol type)
    {
        var stack = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
            stack.Push(current);
        return stack.ToArray();
    }

    private static void AppendTypeDeclaration(StringBuilder sb, INamedTypeSymbol type, string indent)
    {
        sb.Append(indent).Append(GetAccessibility(type.DeclaredAccessibility)).Append(' ');
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            sb.Append("abstract ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ");
        sb.Append(type.IsRecord ? (type.TypeKind == TypeKind.Struct ? "record struct" : "record class") : type.TypeKind == TypeKind.Struct ? "struct" : "class");
        sb.Append(' ').Append(type.Name).AppendLine();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static bool IsValidIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value)
        && SyntaxFacts.IsValidIdentifier(value)
        && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None
        && SyntaxFacts.GetContextualKeywordKind(value) == SyntaxKind.None;

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

    private sealed record StepModel(
        IMethodSymbol Method,
        string Name,
        int Order,
        string Compensation,
        string? Condition);
}
