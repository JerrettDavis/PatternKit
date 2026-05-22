using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class ChannelAdapterGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor MustBePartial = new("PKCAD001", "Channel adapter type must be partial", "Type '{0}' is marked with [GenerateChannelAdapter] but is not declared as partial", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingInbound = new("PKCAD002", "Channel adapter inbound translator is missing", "Type '{0}' must declare exactly one [ChannelAdapterInbound] method", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingOutbound = new("PKCAD003", "Channel adapter outbound translator is missing", "Type '{0}' must declare exactly one [ChannelAdapterOutbound] method", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidInbound = new("PKCAD004", "Channel adapter inbound translator signature is invalid", "Inbound translator '{0}' must be static and return Message<TPayload> with TExternal and MessageContext parameters", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidOutbound = new("PKCAD005", "Channel adapter outbound translator signature is invalid", "Outbound translator '{0}' must be static and return TExternal with Message<TPayload> and MessageContext parameters", "PatternKit.Generators.Messaging", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Messaging.GenerateChannelAdapterAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => (Type: (INamedTypeSymbol)ctx.TargetSymbol, Node: (TypeDeclarationSyntax)ctx.TargetNode, Attributes: ctx.Attributes));

        context.RegisterSourceOutput(candidates, static (spc, candidate) =>
        {
            var attr = candidate.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.GenerateChannelAdapterAttribute");
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

        var externalType = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        var payloadType = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value as INamedTypeSymbol : null;
        if (externalType is null || payloadType is null)
            return;

        var inbound = type.GetMembers().OfType<IMethodSymbol>().Where(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.ChannelAdapterInboundAttribute")).ToArray();
        if (inbound.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingInbound, node.Identifier.GetLocation(), type.Name));
            return;
        }

        var outbound = type.GetMembers().OfType<IMethodSymbol>().Where(static method =>
            method.GetAttributes().Any(static attr => attr.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Messaging.ChannelAdapterOutboundAttribute")).ToArray();
        if (outbound.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingOutbound, node.Identifier.GetLocation(), type.Name));
            return;
        }

        if (!IsInbound(inbound[0], externalType, payloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidInbound, inbound[0].Locations.FirstOrDefault(), inbound[0].Name));
            return;
        }

        if (!IsOutbound(outbound[0], externalType, payloadType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidOutbound, outbound[0].Locations.FirstOrDefault(), outbound[0].Name));
            return;
        }

        var factoryName = GetNamedString(attribute, "FactoryName") ?? "Create";
        var adapterName = GetNamedString(attribute, "AdapterName") ?? "channel-adapter";

        context.AddSource($"{type.Name}.ChannelAdapter.g.cs", SourceText.From(GenerateSource(type, externalType, payloadType, inbound[0].Name, outbound[0].Name, factoryName, adapterName), Encoding.UTF8));
    }

    private static bool IsInbound(IMethodSymbol method, INamedTypeSymbol externalType, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           IsMessageOf(method.ReturnType, payloadType) &&
           method.Parameters.Length == 2 &&
           SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, externalType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsOutbound(IMethodSymbol method, INamedTypeSymbol externalType, INamedTypeSymbol payloadType)
        => method.IsStatic &&
           SymbolEqualityComparer.Default.Equals(method.ReturnType, externalType) &&
           method.Parameters.Length == 2 &&
           IsMessageOf(method.Parameters[0].Type, payloadType) &&
           method.Parameters[1].Type.ToDisplayString() == "PatternKit.Messaging.MessageContext";

    private static bool IsMessageOf(ITypeSymbol type, INamedTypeSymbol payloadType)
        => type is INamedTypeSymbol named &&
           named.ConstructedFrom.ToDisplayString() == "PatternKit.Messaging.Message<TPayload>" &&
           SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], payloadType);

    private static string GenerateSource(INamedTypeSymbol type, INamedTypeSymbol externalType, INamedTypeSymbol payloadType, string inbound, string outbound, string factoryName, string adapterName)
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
        sb.Append("    public static global::PatternKit.Messaging.Adapters.ChannelAdapter<")
            .Append(externalType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(", ")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append("> ").Append(factoryName).AppendLine("(");
        sb.Append("        global::PatternKit.Messaging.Channels.MessageChannel<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine("> inboundChannel,");
        sb.Append("        global::PatternKit.Messaging.Channels.MessageChannel<")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine("> outboundChannel)");
        sb.Append("        => global::PatternKit.Messaging.Adapters.ChannelAdapter<")
            .Append(externalType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(", ")
            .Append(payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append(">.Create(").Append(ToLiteral(adapterName)).AppendLine(")");
        sb.AppendLine("            .ReceiveInto(inboundChannel)");
        sb.AppendLine("            .SendFrom(outboundChannel)");
        sb.Append("            .MapInbound(").Append(inbound).AppendLine(")");
        sb.Append("            .MapOutbound(").Append(outbound).AppendLine(")");
        sb.AppendLine("            .Build();");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static string ToLiteral(string value) => "@\"" + value.Replace("\"", "\"\"") + "\"";
}
