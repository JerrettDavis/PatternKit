using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.MaterializedViews;

[Generator]
public sealed class MaterializedViewGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.MaterializedViews.GenerateMaterializedViewAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.MaterializedViews.MaterializedViewHandlerAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKMV001", "Materialized View host must be partial",
        "Type '{0}' is marked with [GenerateMaterializedView] but is not declared as partial",
        "PatternKit.Generators.MaterializedViews", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingHandlers = new(
        "PKMV002", "Materialized View requires handlers",
        "Type '{0}' must declare at least one [MaterializedViewHandler] method",
        "PatternKit.Generators.MaterializedViews", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKMV003", "Materialized View handler signature is invalid",
        "Method '{0}' must be static and return the state type or ValueTask of the state type with parameters ({1}, handler event) or ({1}, handler event, CancellationToken)",
        "PatternKit.Generators.MaterializedViews", DiagnosticSeverity.Error, true);

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

        var stateType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var eventType = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (stateType is null || eventType is null)
            return;

        var handlers = GetHandlers(type, stateType, eventType, context).ToArray();
        if (handlers.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandlers, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (handlers.Any(static handler => !handler.Valid))
            return;

        var viewName = GetNamedString(attribute, "ViewName");
        if (string.IsNullOrWhiteSpace(viewName))
            viewName = type.Name;

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        context.AddSource($"{type.Name}.MaterializedView.g.cs", SourceText.From(
            GenerateSource(type, stateType, eventType, handlers, factoryName, viewName!),
            Encoding.UTF8));
    }

    private static IEnumerable<HandlerModel> GetHandlers(INamedTypeSymbol type, INamedTypeSymbol stateType, INamedTypeSymbol baseEventType, SourceProductionContext context)
    {
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            var attr = method.GetAttributes().FirstOrDefault(static a => a.AttributeClass?.ToDisplayString() == HandlerAttributeName);
            if (attr is null)
                continue;

            var handlerEventType = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as INamedTypeSymbol : null;
            var order = GetNamedInt(attr, "Order") ?? 0;
            var valid = IsValidHandler(method, stateType, baseEventType, handlerEventType, out var async);
            if (!valid)
                context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, method.Locations.FirstOrDefault(), method.Name, stateType.ToDisplayString()));

            if (handlerEventType is not null)
                yield return new HandlerModel(method.Name, handlerEventType, order, async, valid);
        }
    }

    private static bool IsValidHandler(
        IMethodSymbol method,
        INamedTypeSymbol stateType,
        INamedTypeSymbol baseEventType,
        INamedTypeSymbol? handlerEventType,
        out bool async)
    {
        async = false;
        if (!method.IsStatic || handlerEventType is null)
            return false;
        if (!IsAssignableTo(handlerEventType, baseEventType))
            return false;
        if (method.Parameters.Length is not (2 or 3))
            return false;
        if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stateType))
            return false;
        if (!SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, handlerEventType))
            return false;

        if (method.Parameters.Length == 2 && SymbolEqualityComparer.Default.Equals(method.ReturnType, stateType))
            return true;

        if (method.Parameters.Length == 3 &&
            method.Parameters[2].Type.ToDisplayString() == "System.Threading.CancellationToken" &&
            method.ReturnType is INamedTypeSymbol returnType &&
            returnType.ConstructedFrom.ToDisplayString() == "System.Threading.Tasks.ValueTask<TResult>" &&
            SymbolEqualityComparer.Default.Equals(returnType.TypeArguments[0], stateType))
        {
            async = true;
            return true;
        }

        return false;
    }

    private static bool IsAssignableTo(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }

        return type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, baseType));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol stateType,
        INamedTypeSymbol eventType,
        IReadOnlyList<HandlerModel> handlers,
        string factoryName,
        string viewName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var stateName = stateType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var eventName = eventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.MaterializedViews.MaterializedView<")
            .Append(stateName).Append(", ").Append(eventName).Append("> ").Append(factoryName).AppendLine("()");
        sb.Append(bodyIndent).Append("=> global::PatternKit.Application.MaterializedViews.MaterializedView<")
            .Append(stateName).Append(", ").Append(eventName).Append(">.Create(\"").Append(Escape(viewName)).AppendLine("\")");

        foreach (var handler in handlers.OrderBy(static h => h.Order).ThenBy(static h => h.MethodName))
        {
            var method = handler.Async ? "WithAsyncHandler" : "WithHandler";
            sb.Append(bodyIndent).Append("    .").Append(method).Append('<')
                .Append(handler.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Append(">(").Append(handler.MethodName).Append(", ").Append(handler.Order).AppendLine(")");
        }

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

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static int? GetNamedInt(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as int?;

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

    private sealed record HandlerModel(string MethodName, INamedTypeSymbol EventType, int Order, bool Async, bool Valid);
}
