using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.WorkflowOrchestration;

[Generator]
public sealed class WorkflowOrchestrationGenerator : IIncrementalGenerator
{
    private const string WorkflowAttributeName = "PatternKit.Generators.WorkflowOrchestration.WorkflowOrchestrationAttribute";
    private const string StepAttributeName = "PatternKit.Generators.WorkflowOrchestration.WorkflowStepAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKWO001",
        "Workflow orchestration host must be partial",
        "Type '{0}' is marked with [WorkflowOrchestration] but is not declared as partial",
        "PatternKit.Generators.WorkflowOrchestration",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingSteps = new(
        "PKWO002",
        "Workflow orchestration must declare steps",
        "Workflow orchestration '{0}' must declare at least one [WorkflowStep] method",
        "PatternKit.Generators.WorkflowOrchestration",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidStep = new(
        "PKWO003",
        "Workflow orchestration step signature is invalid",
        "Workflow step '{0}' must accept (TContext, CancellationToken) and return ValueTask",
        "PatternKit.Generators.WorkflowOrchestration",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateStep = new(
        "PKWO004",
        "Workflow orchestration step is duplicated",
        "Workflow orchestration '{0}' has duplicate step names or orders",
        "PatternKit.Generators.WorkflowOrchestration",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKWO005",
        "Workflow orchestration configuration is invalid",
        "Workflow orchestration '{0}' must have non-empty FactoryMethodName and WorkflowName values",
        "PatternKit.Generators.WorkflowOrchestration",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            WorkflowAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == WorkflowAttributeName);
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
        var workflowName = GetNamedString(attribute, "WorkflowName") ?? "workflow-orchestration";
        if (string.IsNullOrWhiteSpace(factoryMethodName) || string.IsNullOrWhiteSpace(workflowName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var steps = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Select(method => (Method: method, Attribute: method.GetAttributes().FirstOrDefault(static step => step.AttributeClass?.ToDisplayString() == StepAttributeName)))
            .Where(static item => item.Attribute is not null)
            .Select(static item => CreateStep(item.Method, item.Attribute!))
            .ToArray();

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
            if (!HasValidStepSignature(step.Method, contextType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidStep, step.Method.Locations.FirstOrDefault(), step.Method.Name));
                return;
            }

            if (!string.IsNullOrWhiteSpace(step.Condition) && !HasValidCondition(type, step.Condition!, contextType!))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidStep, step.Method.Locations.FirstOrDefault(), step.Method.Name));
                return;
            }

            if (!string.IsNullOrWhiteSpace(step.Compensation) && !HasValidStepSignature(FindMethod(type, step.Compensation!), contextType))
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

        context.AddSource($"{type.Name}.WorkflowOrchestration.g.cs", SourceText.From(
            GenerateSource(type, contextType!, factoryMethodName, workflowName, steps.OrderBy(static step => step.Order).ToArray()),
            Encoding.UTF8));
    }

    private static WorkflowStepModel CreateStep(IMethodSymbol method, AttributeData attribute)
    {
        var name = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as string : method.Name;
        var order = attribute.ConstructorArguments.Length >= 2 ? (int)(attribute.ConstructorArguments[1].Value ?? 0) : 0;
        return new WorkflowStepModel(
            method,
            string.IsNullOrWhiteSpace(name) ? method.Name : name!,
            order,
            GetNamedInt(attribute, "MaxAttempts") ?? 1,
            GetNamedString(attribute, "Condition"),
            GetNamedString(attribute, "Compensation"));
    }

    private static bool HasValidStepSignature(IMethodSymbol? method, ITypeSymbol? contextType)
        => method is not null
        && method.Parameters.Length == 2
        && contextType is not null
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextType)
        && method.Parameters[1].Type.ToDisplayString() == "System.Threading.CancellationToken"
        && method.ReturnType.ToDisplayString() == "System.Threading.Tasks.ValueTask";

    private static bool HasValidCondition(INamedTypeSymbol type, string methodName, ITypeSymbol contextType)
    {
        var method = FindMethod(type, methodName);
        return method is not null
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
        string workflowName,
        IReadOnlyList<WorkflowStepModel> steps)
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.WorkflowOrchestration.WorkflowOrchestrator<").Append(contextTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.Append(memberIndent).AppendLine("{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Application.WorkflowOrchestration.WorkflowOrchestrator<").Append(contextTypeName).Append(">.Create(\"").Append(Escape(workflowName)).AppendLine("\")");
        foreach (var step in steps)
        {
            sb.Append(bodyIndent).Append("    .AddStep(\"").Append(Escape(step.Name)).Append("\", static (context, cancellationToken) => ").Append(step.Method.Name).Append("(context, cancellationToken), step => step.At(").Append(step.Order).Append(')');
            if (step.MaxAttempts != 1)
                sb.Append(".WithMaxAttempts(").Append(step.MaxAttempts).Append(')');
            if (!string.IsNullOrWhiteSpace(step.Condition))
                sb.Append(".When(static context => ").Append(step.Condition).Append("(context))");
            if (!string.IsNullOrWhiteSpace(step.Compensation))
                sb.Append(".Compensate(static (context, cancellationToken) => ").Append(step.Compensation).Append("(context, cancellationToken))");
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
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name).AppendLine();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static int? GetNamedInt(AttributeData attribute, string name)
    {
        var value = attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value;
        return value.Value is int integer ? integer : null;
    }

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

    private sealed record WorkflowStepModel(
        IMethodSymbol Method,
        string Name,
        int Order,
        int MaxAttempts,
        string? Condition,
        string? Compensation);
}
