using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class ReliabilityPipelineGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKRP001",
        "Reliability pipeline type must be partial",
        "Type '{0}' is marked with [GenerateReliabilityPipeline] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingHandler = new(
        "PKRP002",
        "Reliability pipeline handler is missing",
        "Type '{0}' is marked with [GenerateReliabilityPipeline] but must declare exactly one [ReliabilityHandler] method",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKRP003",
        "Reliability pipeline handler signature is invalid",
        "Reliability handler '{0}' must be static and return ValueTask<TResult> with Message<TPayload>, MessageContext, and CancellationToken parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidKeySelector = new(
        "PKRP004",
        "Reliability key selector signature is invalid",
        "Reliability key selector '{0}' must be static and return string? with Message<TPayload> and MessageContext parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKRP005",
        "Reliability configuration is invalid",
        "Generated reliability pipeline configuration is invalid: {0}",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateReliabilityPipelineAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateReliabilityPipelineAttribute");
            if (attr is not null)
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

        var payloadType = GetConstructorType(attribute, 0);
        var resultType = GetConstructorType(attribute, 1);
        var outboxPayloadType = GetConstructorType(attribute, 2);
        if (payloadType is null || resultType is null || outboxPayloadType is null)
            return;

        var handlers = GetMarkedMethods(type, "PatternKit.Generators.Messaging.ReliabilityHandlerAttribute");
        if (handlers.Count != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandler, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var handler = handlers[0];
        if (!IsHandler(handler, payloadType, resultType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, handler.Locations.FirstOrDefault(), handler.Name));
            return;
        }

        var keySelectors = GetMarkedMethods(type, "PatternKit.Generators.Messaging.ReliabilityKeySelectorAttribute");
        if (keySelectors.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidKeySelector, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var keySelector = keySelectors.FirstOrDefault();
        if (keySelector is not null && !IsKeySelector(keySelector, payloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidKeySelector, keySelector.Locations.FirstOrDefault(), keySelector.Name));
            return;
        }

        var duplicatePolicy = GetNamedString(attribute, "DuplicatePolicy") ?? "Suppress";
        if (!TryNormalizeDuplicatePolicy(duplicatePolicy, out var normalizedDuplicatePolicy))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), $"unsupported duplicate policy '{duplicatePolicy}'"));
            return;
        }

        var missingKeyPolicy = GetNamedString(attribute, "MissingKeyPolicy") ?? "Reject";
        if (!TryNormalizeMissingKeyPolicy(missingKeyPolicy, out var normalizedMissingKeyPolicy))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), $"unsupported missing key policy '{missingKeyPolicy}'"));
            return;
        }

        var config = new ReliabilityPipelineConfig(
            GetNamedString(attribute, "ReceiverFactoryName") ?? "CreateReceiver",
            GetNamedString(attribute, "InboxFactoryName") ?? "CreateInbox",
            GetNamedString(attribute, "OutboxFactoryName") ?? "CreateOutbox",
            normalizedDuplicatePolicy,
            normalizedMissingKeyPolicy);

        context.AddSource($"{type.Name}.ReliabilityPipeline.g.cs", SourceText.From(
            GenerateSource(type, payloadType, resultType, outboxPayloadType, handler.Name, keySelector?.Name, config),
            Encoding.UTF8));
    }

    private static INamedTypeSymbol? GetConstructorType(AttributeData attribute, int index)
        => attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as INamedTypeSymbol
            : null;

    private static List<IMethodSymbol> GetMarkedMethods(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToList();

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol payloadType, INamedTypeSymbol resultType)
        => method.IsStatic &&
           IsValueTaskOf(method.ReturnType, resultType) &&
           method.Parameters.Length == 3 &&
           IsMessageOf(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static bool IsKeySelector(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.SpecialType == SpecialType.System_String &&
           method.Parameters.Length == 2 &&
           IsMessageOf(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsValueTaskOf(ITypeSymbol type, INamedTypeSymbol resultType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], resultType);

    private static bool IsMessageOf(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        INamedTypeSymbol resultType,
        INamedTypeSymbol outboxPayloadType,
        string handlerName,
        string? keySelectorName,
        ReliabilityPipelineConfig config)
    {
        var payload = payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var result = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var outboxPayload = outboxPayloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

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
        sb.Append("    public static global::PatternKit.Messaging.Reliability.IdempotentReceiver<")
            .Append(payload).Append(", ").Append(result).Append("> ")
            .Append(config.ReceiverFactoryName)
            .AppendLine("(global::PatternKit.Messaging.Reliability.IIdempotencyStore store)");
        sb.Append("        => global::PatternKit.Messaging.Reliability.IdempotentReceiver<")
            .Append(payload).Append(", ").Append(result).AppendLine(">.Create(store, " + handlerName + ")");
        if (keySelectorName is not null)
            sb.Append("            .KeyBy(").Append(keySelectorName).AppendLine(")");
        sb.Append("            .OnDuplicate(global::PatternKit.Messaging.Reliability.DuplicateMessagePolicy.")
            .Append(config.DuplicatePolicy).AppendLine(")");
        sb.Append("            .OnMissingKey(global::PatternKit.Messaging.Reliability.MissingIdempotencyKeyPolicy.")
            .Append(config.MissingKeyPolicy).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine();

        sb.Append("    public static global::PatternKit.Messaging.Reliability.InboxProcessor<")
            .Append(payload).Append(", ").Append(result).Append("> ")
            .Append(config.InboxFactoryName)
            .AppendLine("(global::PatternKit.Messaging.Reliability.IIdempotencyStore store)");
        sb.Append("        => global::PatternKit.Messaging.Reliability.InboxProcessor<")
            .Append(payload).Append(", ").Append(result).AppendLine(">.Create(" + config.ReceiverFactoryName + "(store));");
        sb.AppendLine();

        sb.Append("    public static global::PatternKit.Messaging.Reliability.InMemoryOutbox<")
            .Append(outboxPayload).Append("> ")
            .Append(config.OutboxFactoryName)
            .AppendLine("()");
        sb.Append("        => new global::PatternKit.Messaging.Reliability.InMemoryOutbox<")
            .Append(outboxPayload).AppendLine(">();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static bool TryNormalizeDuplicatePolicy(string value, out string normalized)
    {
        normalized = value;
        if (string.Equals(value, "Suppress", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Suppress";
        else if (string.Equals(value, "ReplayCompleted", System.StringComparison.OrdinalIgnoreCase))
            normalized = "ReplayCompleted";
        else
            return false;

        return true;
    }

    private static bool TryNormalizeMissingKeyPolicy(string value, out string normalized)
    {
        normalized = value;
        if (string.Equals(value, "Reject", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Reject";
        else if (string.Equals(value, "Process", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Process";
        else
            return false;

        return true;
    }

    private readonly record struct ReliabilityPipelineConfig(
        string ReceiverFactoryName,
        string InboxFactoryName,
        string OutboxFactoryName,
        string DuplicatePolicy,
        string MissingKeyPolicy);
}
