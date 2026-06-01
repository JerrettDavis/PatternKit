using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.PortsAndAdapters;

[Generator]
public sealed class PortsAndAdaptersGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.PortsAndAdapters.GeneratePortsAndAdaptersAttribute";
    private const string InboundAttributeName = "PatternKit.Generators.PortsAndAdapters.InboundAdapterAttribute";
    private const string ApplicationAttributeName = "PatternKit.Generators.PortsAndAdapters.ApplicationPortAttribute";
    private const string OutboundAttributeName = "PatternKit.Generators.PortsAndAdapters.OutboundAdapterAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new("PKPA001", "Ports and Adapters host must be partial", "Type '{0}' is marked with [GeneratePortsAndAdapters] but is not declared as partial", "PatternKit.Generators.PortsAndAdapters", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingMethod = new("PKPA002", "Ports and Adapters method is missing", "Ports and Adapters host '{0}' must declare exactly one inbound adapter, application port, and outbound adapter", "PatternKit.Generators.PortsAndAdapters", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidMethod = new("PKPA003", "Ports and Adapters method signature is invalid", "Ports and Adapters method '{0}' has an invalid signature", "PatternKit.Generators.PortsAndAdapters", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidFactoryName = new("PKPA004", "Ports and Adapters factory name is invalid", "Ports and Adapters factory name '{0}' is not a valid identifier", "PatternKit.Generators.PortsAndAdapters", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenerateAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attribute = candidate.Attributes.FirstOrDefault(static attr => attr.AttributeClass?.ToDisplayString() == GenerateAttributeName);
            if (attribute is not null)
                Generate(spc, candidate.Type, candidate.Node, attribute);
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol type, TypeDeclarationSyntax node, AttributeData attribute)
    {
        if (!node.Modifiers.Any(static modifier => modifier.Text == "partial"))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var inboundType = GetTypeArgument(attribute, 0);
        var commandType = GetTypeArgument(attribute, 1);
        var resultType = GetTypeArgument(attribute, 2);
        var outboundType = GetTypeArgument(attribute, 3);
        if (inboundType is null || commandType is null || resultType is null || outboundType is null)
            return;

        var inbound = GetSingleMethod(type, InboundAttributeName);
        var application = GetSingleMethod(type, ApplicationAttributeName);
        var outbound = GetSingleMethod(type, OutboundAttributeName);
        if (inbound is null || application is null || outbound is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingMethod, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (!IsUnary(inbound, inboundType, commandType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMethod, inbound.Locations.FirstOrDefault(), inbound.Name));
            return;
        }

        if (!IsApplicationPort(application, commandType, resultType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMethod, application.Locations.FirstOrDefault(), application.Name));
            return;
        }

        if (!IsUnary(outbound, resultType, outboundType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidMethod, outbound.Locations.FirstOrDefault(), outbound.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        if (!IsIdentifier(factoryName))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidFactoryName, node.Identifier.GetLocation(), factoryName));
            return;
        }

        var pipelineName = GetNamedString(attribute, "PipelineName");
        if (string.IsNullOrWhiteSpace(pipelineName))
            pipelineName = "ports-and-adapters";

        context.AddSource($"{type.Name}.PortsAndAdapters.g.cs", SourceText.From(
            GenerateSource(type, inboundType, commandType, resultType, outboundType, inbound.Name, application.Name, outbound.Name, factoryName, pipelineName!),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol inboundType,
        INamedTypeSymbol commandType,
        INamedTypeSymbol resultType,
        INamedTypeSymbol outboundType,
        string inboundName,
        string applicationName,
        string outboundName,
        string factoryName,
        string pipelineName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var inbound = inboundType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var command = commandType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var result = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var outbound = outboundType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        var memberIndent = indent + "    ";
        var bodyIndent = memberIndent + "    ";
        sb.AppendLine(indent + "{");
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.PortsAndAdapters.PortsAndAdaptersPipeline<")
            .Append(inbound).Append(", ").Append(command).Append(", ").Append(result).Append(", ").Append(outbound).Append("> ")
            .Append(factoryName).AppendLine("()");
        sb.AppendLine(memberIndent + "{");
        sb.Append(bodyIndent).Append("return global::PatternKit.Application.PortsAndAdapters.PortsAndAdaptersPipeline<")
            .Append(inbound).Append(", ").Append(command).Append(", ").Append(result).Append(", ").Append(outbound).Append(">.Create(\"")
            .Append(Escape(pipelineName)).AppendLine("\")");
        sb.Append(bodyIndent).Append("    .AdaptInboundWith(").Append(inboundName).AppendLine(")");
        sb.Append(bodyIndent).Append("    .HandleWith(").Append(applicationName).AppendLine(")");
        sb.Append(bodyIndent).Append("    .AdaptOutboundWith(").Append(outboundName).AppendLine(")");
        sb.AppendLine(bodyIndent + "    .Build();");
        sb.AppendLine(memberIndent + "}");
        sb.AppendLine(indent + "}");
        for (var i = containingTypes.Length - 1; i >= 0; i--)
            sb.AppendLine(new string(' ', i * 4) + "}");

        return sb.ToString();
    }

    private static INamedTypeSymbol[] GetContainingTypes(INamedTypeSymbol type)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();
        for (var current = type.ContainingType; current is not null; current = current.ContainingType)
            containingTypes.Push(current);

        return containingTypes.ToArray();
    }

    private static void AppendTypeDeclaration(StringBuilder sb, INamedTypeSymbol type, int indentLevel)
    {
        sb.Append(new string(' ', indentLevel * 4));
        sb.Append(type.DeclaredAccessibility == Accessibility.Public ? "public " : "internal ");
        if (type.IsStatic)
            sb.Append("static ");
        else if (type.IsSealed && type.TypeKind == TypeKind.Class)
            sb.Append("sealed ");
        sb.Append("partial ").Append(type.TypeKind == TypeKind.Struct ? "struct" : "class").Append(' ').Append(type.Name);
    }

    private static INamedTypeSymbol? GetTypeArgument(AttributeData attribute, int index)
        => attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value as INamedTypeSymbol : null;

    private static IMethodSymbol? GetSingleMethod(INamedTypeSymbol type, string attributeName)
    {
        var methods = type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToArray();
        return methods.Length == 1 ? methods[0] : null;
    }

    private static bool IsUnary(IMethodSymbol method, ITypeSymbol inputType, ITypeSymbol outputType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.Parameters.Length == 1
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, inputType)
        && SymbolEqualityComparer.Default.Equals(method.ReturnType, outputType);

    private static bool IsApplicationPort(IMethodSymbol method, ITypeSymbol commandType, ITypeSymbol resultType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.Parameters.Length == 2
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, commandType)
        && method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken"
        && method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask<" + resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">";

    private static bool IsIdentifier(string value)
        => SyntaxFacts.IsValidIdentifier(value) && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
