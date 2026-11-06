using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.SchedulerAgentSupervisor;

[Generator]
public sealed class SchedulerAgentSupervisorGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PatternKit.Generators.SchedulerAgentSupervisor.GenerateSchedulerAgentSupervisorAttribute";
    private const string AgentAttributeName = "PatternKit.Generators.SchedulerAgentSupervisor.SchedulerAgentAttribute";
    private const string RetryAttributeName = "PatternKit.Generators.SchedulerAgentSupervisor.SchedulerRetryWhenAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKSAS001", "Scheduler Agent Supervisor host must be partial",
        "Type '{0}' is marked with [GenerateSchedulerAgentSupervisor] but is not declared as partial",
        "PatternKit.Generators.SchedulerAgentSupervisor", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingMembers = new(
        "PKSAS002", "Scheduler Agent Supervisor agents are missing",
        "Scheduler Agent Supervisor type '{0}' must declare at least one scheduler agent",
        "PatternKit.Generators.SchedulerAgentSupervisor", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidMember = new(
        "PKSAS003", "Scheduler Agent Supervisor method signature is invalid",
        "Scheduler Agent Supervisor method '{0}' has an invalid static signature",
        "PatternKit.Generators.SchedulerAgentSupervisor", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKSAS004", "Scheduler Agent Supervisor configuration is invalid",
        "Scheduler Agent Supervisor '{0}' must have MaxAttempts > 0 and RetryDelayMilliseconds >= 0",
        "PatternKit.Generators.SchedulerAgentSupervisor", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateAgent = new(
        "PKSAS005", "Scheduler Agent Supervisor agent is duplicated",
        "Scheduler Agent Supervisor agent '{0}' is registered more than once",
        "PatternKit.Generators.SchedulerAgentSupervisor", DiagnosticSeverity.Error, true);

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

        var workType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var resultType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (workType is null || resultType is null)
            return;

        var maxAttempts = GetNamedInt(attribute, "MaxAttempts") ?? 3;
        var retryDelay = GetNamedInt(attribute, "RetryDelayMilliseconds") ?? 1000;
        if (maxAttempts <= 0 || retryDelay < 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var agents = MembersWith(type, AgentAttributeName);
        var retries = MembersWith(type, RetryAttributeName);
        if (agents.Length == 0 || retries.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMembers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        foreach (var agent in agents)
        {
            if (!IsAgent(agent, workType, resultType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidMember, agent.Locations.FirstOrDefault(), agent.Name));
                return;
            }
        }

        if (retries.Length == 1 && !IsRetry(retries[0], workType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMember, retries[0].Locations.FirstOrDefault(), retries[0].Name));
            return;
        }

        var agentNames = agents.Select(GetAgentName).ToArray();
        var duplicate = agentNames.GroupBy(static name => name).FirstOrDefault(static group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateAgent, node.Identifier.GetLocation(), duplicate));
            return;
        }

        context.AddSource($"{type.Name}.SchedulerAgentSupervisor.g.cs", SourceText.From(GenerateSource(
            type,
            workType,
            resultType,
            agents.Zip(agentNames, static (method, name) => (MethodName: method.Name, AgentName: name)).ToArray(),
            retries.FirstOrDefault()?.Name,
            GetNamedString(attribute, "FactoryMethodName") ?? "Create",
            GetNamedString(attribute, "SupervisorName") ?? "scheduler-agent-supervisor",
            maxAttempts,
            retryDelay), Encoding.UTF8));
    }

    private static IMethodSymbol[] MembersWith(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();

    private static bool IsAgent(IMethodSymbol method, INamedTypeSymbol workType, INamedTypeSymbol resultType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, resultType) &&
           method.Parameters.Length == 1 &&
           IsSchedulerContext(method.Parameters[0].Type, workType);

    private static bool IsRetry(IMethodSymbol method, INamedTypeSymbol workType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_Boolean &&
           method.Parameters.Length == 2 &&
           method.Parameters[0].Type.ToDisplayString() == "System.Exception" &&
           IsSchedulerContext(method.Parameters[1].Type, workType);

    private static bool IsSchedulerContext(ITypeSymbol type, INamedTypeSymbol workType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Cloud.SchedulerAgentSupervisor.SchedulerAgentContext<TWork>" &&
           named.TypeArguments.Length == 1 &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], workType);

    private static string GetAgentName(IMethodSymbol method)
        => method.GetAttributes()
            .First(attr => attr.AttributeClass?.ToDisplayString() == AgentAttributeName)
            .ConstructorArguments[0].Value as string ?? method.Name;

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol workType,
        INamedTypeSymbol resultType,
        IReadOnlyList<(string MethodName, string AgentName)> agents,
        string? retryName,
        string factoryMethodName,
        string supervisorName,
        int maxAttempts,
        int retryDelayMilliseconds)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var workTypeName = workType.ToDisplayString(TypeFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Cloud.SchedulerAgentSupervisor.SchedulerAgentSupervisor<").Append(workTypeName).Append(", ").Append(resultTypeName).Append("> ").Append(factoryMethodName).AppendLine("()");
        sb.AppendLine(memberIndent + "{");
        sb.Append(bodyIndent).Append("var policy = global::PatternKit.Cloud.SchedulerAgentSupervisor.SchedulerSupervisionPolicy<").Append(workTypeName).AppendLine(">.Create()");
        sb.Append(bodyIndent).Append("    .MaxAttempts(").Append(maxAttempts).AppendLine(")");
        sb.Append(bodyIndent).Append("    .RetryDelay(global::System.TimeSpan.FromMilliseconds(").Append(retryDelayMilliseconds).AppendLine("))");
        if (retryName is not null)
            sb.Append(bodyIndent).Append("    .RetryWhen(").Append(retryName).AppendLine(")");
        sb.Append(bodyIndent).AppendLine("    .Build();");
        sb.AppendLine();
        sb.Append(bodyIndent).Append("return global::PatternKit.Cloud.SchedulerAgentSupervisor.SchedulerAgentSupervisor<").Append(workTypeName).Append(", ").Append(resultTypeName).Append(">.Create(\"").Append(Escape(supervisorName)).AppendLine("\")");
        sb.Append(bodyIndent).AppendLine("    .Supervision(policy)");
        foreach (var agent in agents)
            sb.Append(bodyIndent).Append("    .Agent(\"").Append(Escape(agent.AgentName)).Append("\", ").Append(agent.MethodName).AppendLine(")");
        sb.Append(bodyIndent).AppendLine("    .Build();");
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

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;

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
}
