using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class MessageEnvelopeGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new(
        "PKME001",
        "Message envelope type must be partial",
        "Type '{0}' is marked with [GenerateMessageEnvelope] but is not declared as partial",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor MissingHeaders = new(
        "PKME002",
        "Message envelope has no required headers",
        "Type '{0}' is marked with [GenerateMessageEnvelope] but does not declare any [MessageEnvelopeHeader] headers",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor InvalidHeader = new(
        "PKME003",
        "Message envelope header is invalid",
        "Message envelope header '{0}' must declare a non-empty name, a value type, and a valid C# parameter name",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateHeader = new(
        "PKME004",
        "Message envelope header is duplicated",
        "Message envelope header '{0}' duplicates another header name or generated parameter name in '{1}'",
        "PatternKit.Generators.Messaging",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateMessageEnvelopeAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateMessageEnvelopeAttribute");
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

        var payloadType = attribute.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (payloadType is null)
            return;

        var headers = GetHeaders(type, context);
        if (headers.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHeaders, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (HasDuplicates(headers, out var duplicate))
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateHeader, duplicate.Location, duplicate.Name, type.Name));
            return;
        }

        var config = new MessageEnvelopeConfig(
            GetNamedString(attribute, "FactoryName") ?? "Create",
            GetNamedString(attribute, "ContextFactoryName") ?? "CreateContext");

        context.AddSource($"{type.Name}.MessageEnvelope.g.cs", SourceText.From(GenerateSource(type, payloadType, headers, config), Encoding.UTF8));
    }

    private static ImmutableArray<Header> GetHeaders(INamedTypeSymbol type, SourceProductionContext context)
    {
        var builder = ImmutableArray.CreateBuilder<Header>();
        foreach (var attr in type.GetAttributes().Where(static attr =>
                     attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.MessageEnvelopeHeaderAttribute"))
        {
            if (!TryGetHeader(attr, out var header))
            {
                var name = attr.ConstructorArguments.Length > 0 ? attr.ConstructorArguments[0].Value as string : null;
                context.ReportDiagnostic(Diagnostic.Create(InvalidHeader, attr.ApplicationSyntaxReference?.GetSyntax().GetLocation(), name ?? type.Name));
                continue;
            }

            builder.Add(header);
        }

        return builder.ToImmutable();
    }

    private static bool TryGetHeader(AttributeData attribute, out Header header)
    {
        header = default;
        if (attribute.ConstructorArguments.Length != 2)
            return false;

        var name = attribute.ConstructorArguments[0].Value as string;
        var valueType = attribute.ConstructorArguments[1].Value as ITypeSymbol;
        if (string.IsNullOrWhiteSpace(name) || valueType is null)
            return false;

        var parameterName = GetNamedString(attribute, "ParameterName") ?? ToParameterName(name!);
        if (!IsValidIdentifier(parameterName))
            return false;

        header = new Header(
            name!,
            parameterName,
            valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation());
        return true;
    }

    private static bool HasDuplicates(IReadOnlyList<Header> headers, out Header duplicate)
    {
        var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var parameters = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var header in headers)
        {
            if (!names.Add(header.Name) || !parameters.Add(header.ParameterName))
            {
                duplicate = header;
                return true;
            }
        }

        duplicate = default;
        return false;
    }

    private static string GenerateSource(
        INamedTypeSymbol type,
        INamedTypeSymbol payloadType,
        IReadOnlyList<Header> headers,
        MessageEnvelopeConfig config)
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

        sb.Append("partial ").Append(GetKind(type)).Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public static global::PatternKit.Messaging.Message<").Append(payload).Append("> ").Append(config.FactoryName).Append('(').Append(payload).Append(" payload");
        foreach (var header in headers)
            sb.Append(", ").Append(header.ValueType).Append(' ').Append(header.ParameterName);
        sb.AppendLine(")");
        sb.Append("        => global::PatternKit.Messaging.Message<").Append(payload).AppendLine(">.Create(payload)");
        foreach (var header in headers)
            sb.Append("            .WithHeader(\"").Append(Escape(header.Name)).Append("\", ").Append(header.ParameterName).AppendLine(")");
        sb.AppendLine("            ;");
        sb.AppendLine();

        sb.Append("    public static global::PatternKit.Messaging.MessageContext ").Append(config.ContextFactoryName)
            .Append("(global::PatternKit.Messaging.Message<").Append(payload).Append("> message, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        if (message is null)");
        sb.AppendLine("            throw new global::System.ArgumentNullException(nameof(message));");
        sb.AppendLine();
        sb.AppendLine("        return global::PatternKit.Messaging.MessageContext.From(message, cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToParameterName(string headerName)
    {
        var sb = new StringBuilder();
        var uppercaseNext = false;
        foreach (var ch in headerName)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(sb.Length == 0 ? char.ToLowerInvariant(ch) : uppercaseNext ? char.ToUpperInvariant(ch) : ch);
                uppercaseNext = false;
            }
            else
            {
                uppercaseNext = sb.Length > 0;
            }
        }

        if (sb.Length == 0 || char.IsDigit(sb[0]))
            sb.Insert(0, "header");

        return sb.ToString();
    }

    private static bool IsValidIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value)
           && SyntaxFacts.IsValidIdentifier(value)
           && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;

    private static string GetKind(INamedTypeSymbol type)
        => type.TypeKind == TypeKind.Struct ? "struct" : "class";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private readonly record struct Header(string Name, string ParameterName, string ValueType, Location? Location);

    private readonly record struct MessageEnvelopeConfig(string FactoryName, string ContextFactoryName);
}
