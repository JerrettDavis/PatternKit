using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class ControlBusGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new("PKCTL001", "Control bus type must be partial", "Type '{0}' is marked with [GenerateControlBus] but is not declared as partial", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingHandlers = new("PKCTL002", "Control bus has no handlers", "Type '{0}' is marked with [GenerateControlBus] but does not declare any [ControlBusCommand] methods", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidHandler = new("PKCTL003", "Control bus handler signature is invalid", "Control bus handler '{0}' must be static and return ControlBusResult<TCommand> with Message<TCommand> and MessageContext parameters", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateHandler = new("PKCTL004", "Control bus command name or order is duplicated", "Control bus command '{0}' duplicates another command name or order in '{1}'", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateControlBusAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateControlBusAttribute");
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

        var commandType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        if (commandType is null)
            return;

        var hasAttributes = type.GetMembers().OfType<IMethodSymbol>().Any(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.ControlBusCommandAttribute"));
        var handlers = GetHandlers(type, commandType, context);
        if (handlers.Length == 0)
        {
            if (!hasAttributes)
                context.ReportDiagnostic(Diagnostic.Create(MissingHandlers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (HasDuplicates(handlers, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateHandler, duplicate.Location, duplicate.CommandName, type.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var busName = GetNamedString(attribute, "BusName") ?? "control-bus";
        context.AddSource($"{type.Name}.ControlBus.g.cs", SourceText.From(
            GenerateSource(type, commandType, handlers.OrderBy(static h => h.Order).ThenBy(static h => h.CommandName).ToArray(), factoryName, busName),
            Encoding.UTF8));
    }

    private static ImmutableArray<Handler> GetHandlers(INamedTypeSymbol type, INamedTypeSymbol commandType, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Handler>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.ControlBusCommandAttribute");
            if (attr is null)
                continue;

            if (!IsHandler(method, commandType) || attr.ConstructorArguments.Length < 2)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            var commandName = attr.ConstructorArguments[0].Value as string;
            var handlerName = attr.ConstructorArguments[1].Value as string;
            var order = attr.ConstructorArguments.Length >= 3 ? attr.ConstructorArguments[2].Value as int? ?? 0 : 0;
            if (string.IsNullOrWhiteSpace(commandName) || string.IsNullOrWhiteSpace(handlerName))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, method.Locations.FirstOrDefault(), method.Name));
                continue;
            }

            builder.Add(new Handler(commandName!, handlerName!, order, method.Name, method.Locations.FirstOrDefault()));
        }

        return builder.ToImmutable();
    }

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol commandType)
        => method.IsStatic &&
           method.Parameters.Length == 2 &&
           IsMessageOfCommand(method.Parameters[0].Type, commandType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext" &&
           IsControlBusResultOfCommand(method.ReturnType, commandType);

    private static bool IsMessageOfCommand(ITypeSymbol type, INamedTypeSymbol commandType)
        => type is INamedTypeSymbol named && named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], commandType);

    private static bool IsControlBusResultOfCommand(ITypeSymbol type, INamedTypeSymbol commandType)
        => type is INamedTypeSymbol named && named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.ControlBus.ControlBusResult<TCommand>" && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], commandType);

    private static bool HasDuplicates(IReadOnlyList<Handler> handlers, out Handler duplicate)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        var orders = new HashSet<int>();
        foreach (var handler in handlers)
        {
            if (!names.Add(handler.CommandName) || !orders.Add(handler.Order))
            {
                duplicate = handler;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(INamedTypeSymbol type, INamedTypeSymbol commandType, IReadOnlyList<Handler> handlers, string factoryName, string busName)
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
        sb.Append("    public static global::PatternKit.Messaging.ControlBus.ControlBus<")
            .Append(commandType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ").Append(factoryName).AppendLine("()");
        sb.Append("        => global::PatternKit.Messaging.ControlBus.ControlBus<")
            .Append(commandType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">.Create(").Append(ToLiteral(busName)).AppendLine(")");
        foreach (var handler in handlers)
            sb.Append("            .Handle(").Append(ToLiteral(handler.CommandName)).Append(", ").Append(ToLiteral(handler.HandlerName)).Append(", ").Append(handler.MethodName).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string ToLiteral(string value) => "@\"" + value.Replace("\"", "\"\"") + "\"";

    private readonly record struct Handler(string CommandName, string HandlerName, int Order, string MethodName, Location? Location);
}
