using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class MessageTranslatorGenerator : IIncrementalGenerator
{
    private const string GenerateAttributeName = "PatternKit.Generators.Messaging.GenerateMessageTranslatorAttribute";
    private const string HandlerAttributeName = "PatternKit.Generators.Messaging.MessageTranslatorHandlerAttribute";
    private const string DropHeaderAttributeName = "PatternKit.Generators.Messaging.MessageTranslatorDropHeaderAttribute";
    private const string SetHeaderAttributeName = "PatternKit.Generators.Messaging.MessageTranslatorHeaderAttribute";

    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKMT001",
        "Message translator host must be partial",
        "Type '{0}' is marked with [GenerateMessageTranslator] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingHandler = new(
        "PKMT002",
        "Message translator handler is missing",
        "Message translator '{0}' must declare exactly one [MessageTranslatorHandler] method",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        "PKMT003",
        "Message translator handler signature is invalid",
        "Message translator handler '{0}' must be static, return the output payload, and accept Message<TInput> plus MessageContext",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

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

        var inputType = attribute.ConstructorArguments.Length >= 1 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var outputType = attribute.ConstructorArguments.Length >= 2 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (inputType is null || outputType is null || inputType.TypeKind == TypeKind.Error || outputType.TypeKind == TypeKind.Error)
            return;

        var handlers = type.GetMembers().OfType<IMethodSymbol>()
            .Where(static method => method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == HandlerAttributeName))
            .ToArray();
        if (handlers.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandler, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var handler = handlers[0];
        if (!IsHandler(handler, inputType, outputType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, handler.Locations.FirstOrDefault(), handler.Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var translatorName = GetNamedString(attribute, "TranslatorName") ?? "message-translator";
        var preserveHeaders = GetNamedBool(attribute, "PreserveHeaders") ?? true;
        var drops = GetHeaderNames(type, DropHeaderAttributeName);
        var sets = GetSetHeaders(type);
        context.AddSource($"{type.Name}.MessageTranslator.g.cs", SourceText.From(
            GenerateSource(type, inputType, outputType, handler.Name, factoryName, translatorName, preserveHeaders, drops, sets),
            Encoding.UTF8));
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol inputType,
        INamedTypeSymbol outputType,
        string handlerName,
        string factoryName,
        string translatorName,
        bool preserveHeaders,
        IReadOnlyList<string> drops,
        IReadOnlyList<HeaderAssignment> sets)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var inputName = inputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var outputName = outputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
        sb.Append(memberIndent).Append("public static global::PatternKit.Messaging.Transformation.MessageTranslator<")
            .Append(inputName).Append(", ").Append(outputName).Append("> ").Append(factoryName).AppendLine("()");
        sb.Append(memberIndent).AppendLine("{");
        sb.Append(bodyIndent).Append("var builder = global::PatternKit.Messaging.Transformation.MessageTranslator<")
            .Append(inputName).Append(", ").Append(outputName).Append(">.Create(\"").Append(Escape(translatorName)).AppendLine("\")");
        sb.Append(bodyIndent).Append("    .PreserveHeaders(").Append(preserveHeaders ? "true" : "false").AppendLine(")");
        sb.Append(bodyIndent).Append("    .TranslateWith(static (message, context) => ").Append(handlerName).AppendLine("(message, context));");

        foreach (var drop in drops)
            sb.Append(bodyIndent).Append("builder.DropHeader(\"").Append(Escape(drop)).AppendLine("\");");
        foreach (var set in sets)
            sb.Append(bodyIndent).Append("builder.SetHeader(\"").Append(Escape(set.Name)).Append("\", \"").Append(Escape(set.Value)).AppendLine("\");");

        sb.Append(bodyIndent).AppendLine("return builder.Build();");
        sb.Append(memberIndent).AppendLine("}");
        sb.Append(indent).AppendLine("}");
        while (indent.Length > 0)
        {
            indent = indent.Substring(0, indent.Length - 4);
            sb.Append(indent).AppendLine("}");
        }
        return sb.ToString();
    }

    private static bool IsHandler(IMethodSymbol method, ITypeSymbol inputType, ITypeSymbol outputType)
        => method.IsStatic
        && !method.IsGenericMethod
        && SymbolEqualityComparer.Default.Equals(method.ReturnType, outputType)
        && method.Parameters.Length == 2
        && IsMessageOfPayload(method.Parameters[0].Type, inputType)
        && method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsMessageOfPayload(ITypeSymbol type, ITypeSymbol payloadType)
        => type is INamedTypeSymbol named
        && named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>"
        && SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static IReadOnlyList<string> GetHeaderNames(INamedTypeSymbol type, string attributeName)
        => type.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == attributeName && attr.ConstructorArguments.Length == 1)
            .Select(attr => attr.ConstructorArguments[0].Value as string)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();

    private static IReadOnlyList<HeaderAssignment> GetSetHeaders(INamedTypeSymbol type)
        => type.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() == SetHeaderAttributeName && attr.ConstructorArguments.Length == 2)
            .Select(static attr => new HeaderAssignment(
                attr.ConstructorArguments[0].Value as string ?? string.Empty,
                attr.ConstructorArguments[1].Value as string ?? string.Empty))
            .Where(static header => !string.IsNullOrWhiteSpace(header.Name))
            .ToArray();

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

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static bool? GetNamedBool(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as bool?;

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

    private readonly record struct HeaderAssignment(string Name, string Value);
}
