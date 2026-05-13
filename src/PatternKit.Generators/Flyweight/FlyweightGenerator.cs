using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators.Flyweight;

[Generator]
public sealed class FlyweightGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                              SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly DiagnosticDescriptor MustBePartial = new("PKFLY001", "Flyweight type must be partial", "Type '{0}' is marked with [Flyweight] but is not declared as partial", "PatternKit.Generators.Flyweight", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MissingFactory = new("PKFLY002", "Flyweight factory method missing", "No [FlyweightFactory] method found for flyweight type '{0}'", "PatternKit.Generators.Flyweight", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor MultipleFactories = new("PKFLY003", "Multiple flyweight factories", "Multiple [FlyweightFactory] methods found for flyweight type '{0}'", "PatternKit.Generators.Flyweight", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidFactory = new("PKFLY004", "Flyweight factory signature is invalid", "Factory method '{0}' must be static and accept one key parameter returning the flyweight type", "PatternKit.Generators.Flyweight", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor NameConflict = new("PKFLY005", "Flyweight cache type name conflicts", "Flyweight cache type name '{0}' conflicts with an existing type in namespace '{1}'", "PatternKit.Generators.Flyweight", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor InvalidEviction = new("PKFLY006", "Invalid flyweight eviction configuration", "Flyweight eviction Lru requires Capacity greater than zero", "PatternKit.Generators.Flyweight", DiagnosticSeverity.Error, true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var flyweights = context.SyntaxProvider.ForAttributeWithMetadataName(
            "PatternKit.Generators.Flyweight.FlyweightAttribute",
            static (node, _) => node is TypeDeclarationSyntax,
            static (ctx, _) => ctx);

        context.RegisterSourceOutput(flyweights, static (spc, ctx) =>
        {
            if (ctx.TargetSymbol is INamedTypeSymbol type)
            {
                var attr = ctx.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Flyweight.FlyweightAttribute");
                if (attr is not null)
                {
                    Generate(spc, type, attr, ctx.TargetNode);
                }
            }
        });
    }

    private static void Generate(SourceProductionContext context, INamedTypeSymbol valueType, AttributeData attr, SyntaxNode node)
    {
        if (!IsPartial(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustBePartial, node.GetLocation(), valueType.Name));
            return;
        }

        var keyType = attr.ConstructorArguments.Length == 1 ? attr.ConstructorArguments[0].Value as INamedTypeSymbol : null;
        if (keyType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidFactory, node.GetLocation(), "<unknown>"));
            return;
        }

        var cacheName = GetString(attr, nameof(FlyweightAttribute.CacheTypeName), valueType.Name + "FlyweightCache");
        var capacity = GetInt(attr, nameof(FlyweightAttribute.Capacity));
        var eviction = GetEnumName(attr, nameof(FlyweightAttribute.Eviction), "None");
        var generateTryGet = GetBool(attr, nameof(FlyweightAttribute.GenerateTryGet), true);

        if (eviction == "Lru" && capacity <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidEviction, node.GetLocation()));
            return;
        }

        if (valueType.ContainingNamespace.GetTypeMembers(cacheName).Any())
        {
            context.ReportDiagnostic(Diagnostic.Create(NameConflict, node.GetLocation(), cacheName, valueType.ContainingNamespace.ToDisplayString()));
            return;
        }

        var factories = valueType.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Flyweight.FlyweightFactoryAttribute"))
            .ToArray();

        if (factories.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingFactory, node.GetLocation(), valueType.Name));
            return;
        }
        if (factories.Length > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(MultipleFactories, node.GetLocation(), valueType.Name));
            return;
        }

        var factory = factories[0];
        if (!factory.IsStatic || factory.Parameters.Length != 1 ||
            !SymbolEqualityComparer.Default.Equals(factory.Parameters[0].Type, keyType) ||
            !SymbolEqualityComparer.Default.Equals(factory.ReturnType, valueType))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidFactory, node.GetLocation(), factory.Name));
            return;
        }

        context.AddSource($"{valueType.Name}.Flyweight.g.cs", Render(valueType, keyType, factory, cacheName, capacity, eviction == "Lru", generateTryGet));
    }

    private static string Render(INamedTypeSymbol valueType, INamedTypeSymbol keyType, IMethodSymbol factory, string cacheName, int capacity, bool lru, bool generateTryGet)
    {
        var ns = valueType.ContainingNamespace.IsGlobalNamespace ? null : valueType.ContainingNamespace.ToDisplayString();
        var key = keyType.ToDisplayString(TypeFormat);
        var value = valueType.ToDisplayString(TypeFormat);
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        if (ns is not null)
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }
        sb.Append("partial ").Append(GetTypeDeclarationKind(valueType)).Append(' ').Append(valueType.Name).AppendLine();
        sb.AppendLine("{");
        sb.Append("    internal static ").Append(value).Append(" __PatternKitFlyweightCreate(").Append(key).Append(" key) => ").Append(factory.Name).Append("(key);").AppendLine();
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("public sealed partial class ").Append(cacheName).AppendLine();
        sb.AppendLine("{");
        sb.Append("    private readonly global::System.Collections.Generic.Dictionary<").Append(key).Append(", ").Append(value).Append("> _cache;").AppendLine();
        sb.AppendLine("    private readonly object _gate = new();");
        if (lru)
        {
            sb.Append("    private readonly global::System.Collections.Generic.LinkedList<").Append(key).Append("> _lru = new();").AppendLine();
            sb.Append("    private readonly global::System.Collections.Generic.Dictionary<").Append(key).Append(", global::System.Collections.Generic.LinkedListNode<").Append(key).Append(">> _nodes;").AppendLine();
        }
        sb.AppendLine();
        sb.Append("    public ").Append(cacheName).Append("(global::System.Collections.Generic.IEqualityComparer<").Append(key).Append(">? comparer = null)").AppendLine();
        sb.AppendLine("    {");
        sb.Append("        _cache = new global::System.Collections.Generic.Dictionary<").Append(key).Append(", ").Append(value).Append(">(comparer);").AppendLine();
        if (lru)
        {
            sb.Append("        _nodes = new global::System.Collections.Generic.Dictionary<").Append(key).Append(", global::System.Collections.Generic.LinkedListNode<").Append(key).Append(">>(comparer);").AppendLine();
        }
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    public ").Append(value).Append(" Get(").Append(key).Append(" key)").AppendLine();
        sb.AppendLine("    {");
        sb.AppendLine("        lock (_gate)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_cache.TryGetValue(key, out var existing))");
        sb.AppendLine("            {");
        if (lru) sb.AppendLine("                Touch(key);");
        sb.AppendLine("                return existing;");
        sb.AppendLine("            }");
        sb.Append("            var created = ").Append(valueType.ToDisplayString(TypeFormat)).Append(".__PatternKitFlyweightCreate(key);").AppendLine();
        sb.AppendLine("            _cache.Add(key, created);");
        if (lru)
        {
            sb.AppendLine("            AddLru(key);");
            sb.AppendLine("            Trim();");
        }
        sb.AppendLine("            return created;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        if (generateTryGet)
        {
            sb.AppendLine();
            sb.Append("    public bool TryGet(").Append(key).Append(" key, out ").Append(value).Append(" value)").AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine("        lock (_gate)");
            sb.AppendLine("        {");
            sb.AppendLine("            var found = _cache.TryGetValue(key, out value);");
            if (lru) sb.AppendLine("            if (found) Touch(key);");
            sb.AppendLine("            return found;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
        sb.AppendLine();
        sb.AppendLine("    public void Clear()");
        sb.AppendLine("    {");
        sb.AppendLine("        lock (_gate)");
        sb.AppendLine("        {");
        sb.AppendLine("            _cache.Clear();");
        if (lru)
        {
            sb.AppendLine("            _lru.Clear();");
            sb.AppendLine("            _nodes.Clear();");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        if (lru)
        {
            sb.AppendLine();
            sb.AppendLine("    private void Touch(" + key + " key)");
            sb.AppendLine("    {");
            sb.AppendLine("        var node = _nodes[key];");
            sb.AppendLine("        _lru.Remove(node);");
            sb.AppendLine("        _lru.AddFirst(node);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void AddLru(" + key + " key)");
            sb.AppendLine("    {");
            sb.AppendLine("        var node = new global::System.Collections.Generic.LinkedListNode<" + key + ">(key);");
            sb.AppendLine("        _lru.AddFirst(node);");
            sb.AppendLine("        _nodes.Add(key, node);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void Trim()");
            sb.AppendLine("    {");
            sb.AppendLine("        while (_cache.Count > " + capacity + ")");
            sb.AppendLine("        {");
            sb.AppendLine("            var last = _lru.Last!;");
            sb.AppendLine("            _lru.RemoveLast();");
            sb.AppendLine("            _nodes.Remove(last.Value);");
            sb.AppendLine("            _cache.Remove(last.Value);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static bool IsPartial(SyntaxNode node) => node is TypeDeclarationSyntax type && type.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static string GetTypeDeclarationKind(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Struct)
        {
            return type.IsRecord ? "record struct" : "struct";
        }
        return type.IsRecord ? "record class" : "class";
    }

    private static string GetString(AttributeData attr, string name, string fallback) =>
        attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value as string ?? fallback;

    private static int GetInt(AttributeData attr, string name) =>
        attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is int value ? value : 0;

    private static bool GetBool(AttributeData attr, string name, bool fallback) =>
        attr.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value is bool value ? value : fallback;

    private static string GetEnumName(AttributeData attr, string name, string fallback)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == name && arg.Value.Value is int value)
            {
                return value == 1 ? "Lru" : "None";
            }
        }
        return fallback;
    }
}
