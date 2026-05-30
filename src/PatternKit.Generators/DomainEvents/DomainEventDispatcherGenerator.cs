using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.DomainEvents;

[Generator]
public sealed class DomainEventDispatcherGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.DomainEvents.GenerateDomainEventDispatcherAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.DomainEvents.DomainEventHandlerAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKDE001", "Domain Event host must be partial",
        "Type '{0}' is marked with [GenerateDomainEventDispatcher] but is not declared as partial",
        "PatternKit.Generators.DomainEvents", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor MissingHandler = new(
        "PKDE002", "Domain Event handlers are missing",
        "Domain Event dispatcher '{0}' must declare at least one [DomainEventHandler] method",
        "PatternKit.Generators.DomainEvents", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKDE003", "Domain Event handler signature is invalid",
        "Domain Event handler '{0}' must be static and return ValueTask from event type and CancellationToken parameters",
        "PatternKit.Generators.DomainEvents", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DuplicateOrder = new(
        "PKDE004", "Domain Event handler order is duplicated",
        "Domain Event handler order values must be unique per event type",
        "PatternKit.Generators.DomainEvents", DiagnosticSeverity.Error, true);

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

        var eventBaseType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        if (eventBaseType is null)
            return;

        var handlers = GetHandlers(type);
        if (handlers.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandler, node.Identifier.GetLocation(), type.Name));
            return;
        }

        foreach (var group in handlers.GroupBy(static handler => handler.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
        {
            if (group.Select(static handler => handler.Order).Distinct().Count() != group.Count())
            {
                context.ReportDiagnostic(Diagnostic.Create(DuplicateOrder, node.Identifier.GetLocation()));
                return;
            }
        }

        foreach (var handler in handlers)
        {
            if (!IsHandler(handler.Method, handler.EventType, eventBaseType))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, handler.Method.Locations.FirstOrDefault(), handler.Method.Name));
                return;
            }
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var dispatcherName = GetNamedString(attribute, "DispatcherName");
        if (string.IsNullOrWhiteSpace(dispatcherName))
            dispatcherName = type.Name;

        context.AddSource($"{type.Name}.DomainEvents.g.cs", SourceText.From(
            GenerateSource(type, eventBaseType, handlers
                .OrderBy(static handler => handler.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ThenBy(static handler => handler.Order)
                .ToArray(), factoryName, dispatcherName!),
            Encoding.UTF8));
    }

    private static List<HandlerConfig> GetHandlers(INamedTypeSymbol type)
    {
        var handlers = new List<HandlerConfig>();
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            foreach (var attribute in method.GetAttributes().Where(static attr => attr.AttributeClass?.ToDisplayString() == HandlerAttributeName))
            {
                if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol eventType)
                    handlers.Add(new HandlerConfig(method, eventType, (int)(attribute.ConstructorArguments[1].Value ?? 0)));
            }
        }

        return handlers;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol eventBaseType,
        IReadOnlyList<HandlerConfig> handlers,
        string factoryName,
        string dispatcherName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var eventBaseName = eventBaseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Application.DomainEvents.DomainEventDispatcher<")
            .Append(eventBaseName).Append("> ").Append(factoryName).AppendLine("()");
        sb.Append(memberIndent).AppendLine("{");
        sb.Append(bodyIndent).Append("var builder = global::PatternKit.Application.DomainEvents.DomainEventDispatcher<")
            .Append(eventBaseName).Append(">.Create(\"").Append(Escape(dispatcherName)).AppendLine("\");");
        foreach (var handler in handlers)
        {
            var eventTypeName = handler.EventType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.Append(bodyIndent).Append("builder.Handle<").Append(eventTypeName).Append(">(").Append(handler.Method.Name).AppendLine(");");
        }

        sb.Append(bodyIndent).AppendLine("return builder.Build();");
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

    private static bool IsHandler(IMethodSymbol method, INamedTypeSymbol eventType, INamedTypeSymbol eventBaseType)
        => method.IsStatic
        && !method.IsGenericMethod
        && method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask"
        && method.Parameters.Length == 2
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, eventType)
        && InheritsFrom(eventType, eventBaseType)
        && method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.CancellationToken";

    private static bool InheritsFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }

        return type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, baseType));
    }

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

    private sealed record HandlerConfig(IMethodSymbol Method, INamedTypeSymbol EventType, int Order);
}
