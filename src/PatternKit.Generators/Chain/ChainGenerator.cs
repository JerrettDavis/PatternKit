using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PatternKit.Generators.Chain;

[Generator]
public sealed class ChainGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                              SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new("PKCH001", "Chain type must be partial", "Type '{0}' is marked with [Chain] but is not declared as partial", "PatternKit.Generators.Chain", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingHandlers = new("PKCH002", "No chain handlers found", "No [ChainHandler] methods found for chain type '{0}'", "PatternKit.Generators.Chain", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateOrder = new("PKCH003", "Duplicate chain handler order", "Multiple chain handlers use Order {0}", "PatternKit.Generators.Chain", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidHandler = new("PKCH004", "Chain handler signature invalid", "Handler method '{0}' must return bool and accept (in TInput, out TOutput)", "PatternKit.Generators.Chain", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingTerminal = new("PKCH005", "Pipeline terminal missing", "Pipeline chains require a [ChainTerminal] method", "PatternKit.Generators.Chain", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MultipleTerminals = new("PKCH006", "Multiple pipeline terminals", "Pipeline chains cannot declare multiple [ChainTerminal] methods", "PatternKit.Generators.Chain", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingDefault = new("PKCH007", "Chain default missing", "Responsibility chain '{0}' must declare a [ChainDefault] method for Handle fallback", "PatternKit.Generators.Chain", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var chains = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Chain.ChainAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => ctx);

        context.RegisterSourceOutput(chains, static (spc, ctx) =>
        {
            if (ctx.TargetSymbol is INamedTypeSymbol type)
            {
                var attr = ctx.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Chain.ChainAttribute");
                if (attr is not null) Generate(spc, type, attr, ctx.TargetNode);
            }
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol type, AttributeData attr, SyntaxNode node)
    {
        if (!IsPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.GetLocation(), type.Name));
            return;
        }

        var model = GetEnum(attr, nameof(ChainAttribute.Model));
        if (model == 1)
        {
            var terminals = GetMethods(type, "PatternKit.Generators.Chain.ChainTerminalAttribute").ToArray();
            context.ReportDiagnostic(Diagnostic.Create(terminals.Length > 1 ? MultipleTerminals : MissingTerminal, node.GetLocation()));
            return;
        }

        var handlers = GetMethods(type, "PatternKit.Generators.Chain.ChainHandlerAttribute")
            .Select(m => new Handler(m, GetOrder(m)))
            .OrderBy(h => h.Order)
            .ThenBy(h => h.Method.Name, StringComparer.Ordinal)
            .ToArray();

        if (handlers.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingHandlers, node.GetLocation(), type.Name));
            return;
        }
        var dup = handlers.GroupBy(h => h.Order).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DuplicateOrder, node.GetLocation(), dup.Key));
            return;
        }

        if (!TryGetShape(handlers[0].Method, out var input, out var output) || handlers.Any(h => !SameShape(h.Method, input!, output!)))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, node.GetLocation(), handlers.First().Method.Name));
            return;
        }

        var defaults = GetMethods(type, "PatternKit.Generators.Chain.ChainDefaultAttribute").ToArray();
        if (defaults.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingDefault, node.GetLocation(), type.Name));
            return;
        }
        var fallback = defaults[0];
        if (!SymbolEqualityComparer.Default.Equals(fallback.ReturnType, output) || fallback.Parameters.Length != 1 ||
            !SymbolEqualityComparer.Default.Equals(fallback.Parameters[0].Type, input))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidHandler, node.GetLocation(), fallback.Name));
            return;
        }

        var handleName = GetString(attr, nameof(ChainAttribute.HandleMethodName), "Handle");
        var tryName = GetString(attr, nameof(ChainAttribute.TryHandleMethodName), "TryHandle");
        context.AddSource($"{type.Name}.Chain.g.cs", Render(type, handlers, fallback, input!, output!, handleName, tryName));
    }

    private static string Render(INamedTypeSymbol type, Handler[] handlers, IMethodSymbol fallback, ITypeSymbol input, ITypeSymbol output, string handleName, string tryName)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }
        sb.Append("public partial ").Append(GetKind(type)).Append(' ').Append(type.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    public bool ").Append(tryName).Append("(in ").Append(input.ToDisplayString(TypeFormat)).Append(" input, out ").Append(output.ToDisplayString(TypeFormat)).Append(" output)").AppendLine();
        sb.AppendLine("    {");
        foreach (var h in handlers)
        {
            sb.Append("        if (").Append(h.Method.Name).Append("(in input, out output)) return true;").AppendLine();
        }
        sb.AppendLine("        output = default!;");
        sb.AppendLine("        return false;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public ").Append(output.ToDisplayString(TypeFormat)).Append(' ').Append(handleName).Append("(in ").Append(input.ToDisplayString(TypeFormat)).Append(" input)").AppendLine();
        sb.AppendLine("    {");
        sb.Append("        return ").Append(tryName).Append("(in input, out var output) ? output : ").Append(fallback.Name).Append("(in input);").AppendLine();
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool TryGetShape(IMethodSymbol method, out ITypeSymbol? input, out ITypeSymbol? output)
    {
        input = null;
        output = null;
        if (method.ReturnType.SpecialType != SpecialType.System_Boolean || method.Parameters.Length != 2 ||
            method.Parameters[0].RefKind != RefKind.In || method.Parameters[1].RefKind != RefKind.Out)
        {
            return false;
        }
        input = method.Parameters[0].Type;
        output = method.Parameters[1].Type;
        return true;
    }

    private static bool SameShape(IMethodSymbol method, ITypeSymbol input, ITypeSymbol output) =>
        TryGetShape(method, out var i, out var o) &&
        SymbolEqualityComparer.Default.Equals(i, input) &&
        SymbolEqualityComparer.Default.Equals(o, output);

    private static IEnumerable<IMethodSymbol> GetMethods(INamedTypeSymbol type, string attr) =>
        type.GetMembers().OfType<IMethodSymbol>().Where(m => m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attr));

    private static int GetOrder(IMethodSymbol method)
    {
        var attr = method.GetAttributes().First(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Chain.ChainHandlerAttribute");
        return attr.NamedArguments.FirstOrDefault(a => a.Key == nameof(ChainHandlerAttribute.Order)).Value.Value is int value ? value : 0;
    }

    private static bool IsPartial(SyntaxNode node) => node is TypeDeclarationSyntax type && type.Modifiers.Any(SyntaxKind.PartialKeyword);
    private static int GetEnum(AttributeData attr, string name) => attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is int value ? value : 0;
    private static string GetString(AttributeData attr, string name, string fallback) => attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value as string ?? fallback;
    private static string GetKind(INamedTypeSymbol type) => type.TypeKind == TypeKind.Struct ? (type.IsRecord ? "record struct" : "struct") : (type.IsRecord ? "record class" : "class");

    private readonly record struct Handler(IMethodSymbol Method, int Order);
}
