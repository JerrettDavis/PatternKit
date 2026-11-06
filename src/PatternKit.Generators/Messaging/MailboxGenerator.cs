using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class MailboxGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKMB001",
        "Mailbox type must be partial",
        "Type '{0}' is marked with [GenerateMailbox] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingHandler = new(
        "PKMB002",
        "Mailbox handler is missing",
        "Type '{0}' is marked with [GenerateMailbox] but must declare exactly one [MailboxHandler] method",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKMB003",
        "Mailbox handler signature is invalid",
        "Mailbox handler '{0}' must be static and return ValueTask with Message<TPayload>, MessageContext, and CancellationToken parameters",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidOptionalHandler = new(
        "PKMB004",
        "Mailbox optional handler signature is invalid",
        "Mailbox optional handler '{0}' has an invalid error-handler or event-sink signature",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidConfiguration = new(
        "PKMB005",
        "Mailbox configuration is invalid",
        "Generated mailbox configuration is invalid: {0}",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateMailboxAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateMailboxAttribute");
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

        var payloadType = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (payloadType is null)
            return;

        var handlers = GetMarkedMethods(type, "PatternKit.Generators.Messaging.MailboxHandlerAttribute");
        if (handlers.Count != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandler, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var handler = handlers[0];
        if (!IsHandler(handler, payloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, handler.Locations.FirstOrDefault(), handler.Name));
            return;
        }

        var errorHandlers = GetMarkedMethods(type, "PatternKit.Generators.Messaging.MailboxErrorHandlerAttribute");
        var eventSinks = GetMarkedMethods(type, "PatternKit.Generators.Messaging.MailboxEventSinkAttribute");
        if (errorHandlers.Count > 1 || eventSinks.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidOptionalHandler, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var errorHandler = errorHandlers.FirstOrDefault();
        if (errorHandler is not null && !IsErrorHandler(errorHandler, payloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidOptionalHandler, errorHandler.Locations.FirstOrDefault(), errorHandler.Name));
            return;
        }

        var eventSink = eventSinks.FirstOrDefault();
        if (eventSink is not null && !IsEventSink(eventSink))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidOptionalHandler, eventSink.Locations.FirstOrDefault(), eventSink.Name));
            return;
        }

        var capacity = GetNamedInt(attribute, "Capacity");
        if (capacity < 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), "capacity must be zero or greater"));
            return;
        }

        var backpressure = GetNamedString(attribute, "BackpressurePolicy") ?? "Wait";
        if (!TryNormalizeBackpressure(backpressure, out var normalizedBackpressure))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), $"unsupported backpressure policy '{backpressure}'"));
            return;
        }

        var errorPolicy = GetNamedString(attribute, "ErrorPolicy") ?? "Stop";
        if (!TryNormalizeErrorPolicy(errorPolicy, out var normalizedErrorPolicy))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidConfiguration, node.Identifier.GetLocation(), $"unsupported error policy '{errorPolicy}'"));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        context.AddSource($"{type.Name}.Mailbox.g.cs", SourceText.From(
            GenerateSource(type, payloadType, handler.Name, errorHandler?.Name, eventSink?.Name, capacity, normalizedBackpressure, normalizedErrorPolicy, factoryName),
            Encoding.UTF8));
    }

    private static List<IMethodSymbol> GetMarkedMethods(INamedTypeSymbol type, string attributeName)
        => type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.GetAttributes().Any(attr => attr.AttributeClass?.ToDisplayString() == attributeName))
            .ToList();

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.ToDisplayString() == "System.Threading.Tasks.ValueTask" &&
           method.Parameters.Length == 3 &&
           IsMessageOf(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static bool IsErrorHandler(IMethodSymbol method, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           method.ReturnType.ToDisplayString() == "System.Threading.Tasks.ValueTask" &&
           method.Parameters.Length == 4 &&
           method.Parameters[0].Type.ToDisplayString() == "System.Exception" &&
           IsMessageOf(method.Parameters[1].Type, payloadType) &&
           method.Parameters[2].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           method.Parameters[3].Type.ToDisplayString() == "System.Threading.CancellationToken";

    private static bool IsEventSink(IMethodSymbol method)
        => method.IsStatic &&
           method.ReturnsVoid &&
           method.Parameters.Length == 1 &&
           method.Parameters[0].Type.ToDisplayString() == "PatternKit.Messaging.Mailboxes.MailboxEvent";

    private static bool IsMessageOf(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        string handlerName,
        string? errorHandlerName,
        string? eventSinkName,
        int capacity,
        string backpressurePolicy,
        string errorPolicy,
        string factoryName)
    {
        var payload = payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Messaging.Mailboxes.Mailbox<")
            .Append(payload)
            .Append("> ")
            .Append(factoryName)
            .AppendLine("()");
        sb.Append(bodyIndent).Append("=> global::PatternKit.Messaging.Mailboxes.Mailbox<")
            .Append(payload)
            .AppendLine(">.Create(" + handlerName + ")");

        if (capacity > 0)
        {
            sb.Append(bodyIndent).Append("    .Bounded(")
                .Append(capacity)
                .Append(", global::PatternKit.Messaging.Mailboxes.MailboxBackpressurePolicy.")
                .Append(backpressurePolicy)
                .AppendLine(")");
        }
        else
        {
            sb.Append(bodyIndent).AppendLine("    .Unbounded()");
        }

        sb.Append(bodyIndent).Append("    .OnError(global::PatternKit.Messaging.Mailboxes.MailboxErrorPolicy.")
            .Append(errorPolicy);
        if (errorHandlerName is not null)
            sb.Append(", ").Append(errorHandlerName);
        sb.AppendLine(")");

        if (eventSinkName is not null)
            sb.Append(bodyIndent).Append("    .OnEvent(").Append(eventSinkName).AppendLine(")");

        sb.Append(bodyIndent).AppendLine("    .Build();");
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

    private static int GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int? ?? 0;

    private static bool TryNormalizeBackpressure(string value, out string normalized)
    {
        normalized = value;
        if (string.Equals(value, "Wait", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Wait";
        else if (string.Equals(value, "Reject", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Reject";
        else if (string.Equals(value, "DropNewest", System.StringComparison.OrdinalIgnoreCase))
            normalized = "DropNewest";
        else if (string.Equals(value, "DropOldest", System.StringComparison.OrdinalIgnoreCase))
            normalized = "DropOldest";
        else
            return false;

        return true;
    }

    private static bool TryNormalizeErrorPolicy(string value, out string normalized)
    {
        normalized = value;
        if (string.Equals(value, "Stop", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Stop";
        else if (string.Equals(value, "Continue", System.StringComparison.OrdinalIgnoreCase))
            normalized = "Continue";
        else
            return false;

        return true;
    }
}
