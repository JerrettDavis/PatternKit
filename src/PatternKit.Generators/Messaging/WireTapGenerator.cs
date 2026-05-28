using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class WireTapGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKWT001",
        "Wire tap type must be partial",
        "Type '{0}' is marked with [GenerateWireTap] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingHandlers = new(
        "PKWT002",
        "Wire tap has no handlers",
        "Type '{0}' is marked with [GenerateWireTap] but does not declare any [WireTapHandler] methods",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKWT003",
        "Wire tap handler signature is invalid",
        "Wire tap handler '{0}' must be static void with Message<TPayload> and MessageContext parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateHandler = new(
        "PKWT004",
        "Wire tap handler name or order is duplicated",
        "Wire tap handler '{0}' duplicates another handler name or order in '{1}'",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateWireTapAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateWireTapAttribute");
            if (attr is null)
                return;

            Generate(spc, candidate.Type, candidate.Node, attr);
        });
    }

    private static void Generate(
        SourceProductionContext context,
        INamedTypeSymbol type,
        TypeDeclarationSyntax node,
        AttributeData attribute)
    {
        if (!node.Modifiers.Any(static modifier => modifier.Text == "partial"))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var payloadType = attribute.ConstructorArguments.Length >= 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (payloadType is null)
            return;

        var hasHandlerAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr =>
                attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.WireTapHandlerAttribute"));
        var handlers = GetHandlers(type, payloadType, context);
        if (handlers.Length == 0)
        {
            if (!hasHandlerAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingHandlers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (HasDuplicates(handlers, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateHandler, duplicate.Location, duplicate.Name, type.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var tapName = GetNamedString(attribute, "TapName") ?? "wire-tap";
        var ordered = handlers.OrderBy(static handler => handler.Order).ThenBy(static handler => handler.Name).ToArray();

        context.AddSource($"{type.Name}.WireTap.g.cs", SourceText.From(
            GenerateSource(type, payloadType, ordered, factoryName, tapName),
            Encoding.UTF8));
    }

    private static ImmutableArray<TapHandler> GetHandlers(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<TapHandler>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.WireTapHandlerAttribute");
            if (attr is null)
                continue;

            if (!TryGetHandler(method, payloadType, attr, out var handler))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(handler);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetHandler(
        IMethodSymbol method,
        INamedTypeSymbol payloadType,
        AttributeData attribute,
        out TapHandler handler)
    {
        handler = default;
        if (!IsHandler(method, payloadType) || attribute.ConstructorArguments.Length != 2)
            return false;

        var name = attribute.ConstructorArguments[0].Value as string;
        var order = attribute.ConstructorArguments[1].Value as int? ?? 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        handler = new TapHandler(name!, order, method.Name, method.Locations.FirstOrDefault());
        return true;
    }

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 2 &&
           IsMessageOfPayload(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsMessageOfPayload(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static bool HasDuplicates(IReadOnlyList<TapHandler> handlers, out TapHandler duplicate)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        var orders = new HashSet<int>();
        foreach (var handler in handlers)
        {
            if (!names.Add(handler.Name) || !orders.Add(handler.Order))
            {
                duplicate = handler;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        IReadOnlyList<TapHandler> handlers,
        string factoryName,
        string tapName)
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

        sb.Append("partial ").Append(GetKind(type)).Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public static global::PatternKit.Messaging.Routing.WireTap<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.Routing.WireTap<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">.Create(")
            .Append(ToLiteral(tapName))
            .AppendLine(")");

        foreach (var handler in handlers)
            sb.Append("            .AddTap(").Append(ToLiteral(handler.Name)).Append(", ").Append(handler.MethodName).AppendLine(")");

        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string ToLiteral(string value)
        => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private readonly record struct TapHandler(string Name, int Order, string MethodName, Location? Location);
}
