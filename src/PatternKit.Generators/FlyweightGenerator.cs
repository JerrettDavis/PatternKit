using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Flyweight pattern.
/// Generates cache classes with Get, optional TryGet, and Clear methods
/// that ensure a single shared instance per key.
/// </summary>
[Generator]
public sealed class FlyweightGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagIdTypeNotPartial = "PKFLY001";
    private const string DiagIdNoFactory = "PKFLY002";
    private const string DiagIdMultipleFactories = "PKFLY003";
    private const string DiagIdInvalidFactorySignature = "PKFLY004";
    private const string DiagIdCacheNameConflict = "PKFLY005";
    private const string DiagIdInvalidEvictionConfig = "PKFLY006";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Flyweight] must be partial",
        messageFormat: "Type '{0}' is marked with [Flyweight] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Flyweight",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoFactoryDescriptor = new(
        id: DiagIdNoFactory,
        title: "No factory method found",
        messageFormat: "Type '{0}' has [Flyweight] but no method marked with [FlyweightFactory]. A static factory method is required.",
        category: "PatternKit.Generators.Flyweight",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MultipleFactoriesDescriptor = new(
        id: DiagIdMultipleFactories,
        title: "Multiple factory methods found",
        messageFormat: "Type '{0}' has multiple methods marked with [FlyweightFactory]. Only one factory method is allowed.",
        category: "PatternKit.Generators.Flyweight",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidFactorySignatureDescriptor = new(
        id: DiagIdInvalidFactorySignature,
        title: "Invalid factory method signature",
        messageFormat: "Factory method '{0}' must be static, accept exactly one parameter of the key type, and return the value type.",
        category: "PatternKit.Generators.Flyweight",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CacheNameConflictDescriptor = new(
        id: DiagIdCacheNameConflict,
        title: "Cache type name conflicts with existing type",
        messageFormat: "The cache type name '{0}' conflicts with an existing type in the same namespace. Use the CacheTypeName property to specify a different name.",
        category: "PatternKit.Generators.Flyweight",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidEvictionConfigDescriptor = new(
        id: DiagIdInvalidEvictionConfig,
        title: "Invalid eviction configuration",
        messageFormat: "LRU eviction requires a Capacity greater than 0. Set Capacity to a positive value or use Eviction = None.",
        category: "PatternKit.Generators.Flyweight",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var flyweightTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Flyweight.FlyweightAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(flyweightTypes, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Flyweight.FlyweightAttribute");
            if (attr is null)
                return;

            GenerateFlyweightForType(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateFlyweightForType(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        // Check if type is partial
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Parse attribute configuration
        var config = ParseFlyweightConfig(attribute);

        // Validate eviction config
        if (config.Eviction == 1 && config.Capacity <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidEvictionConfigDescriptor,
                node.GetLocation()));
            return;
        }

        // Find factory methods
        var factories = CollectFactoryMethods(typeSymbol);

        if (factories.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoFactoryDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        if (factories.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MultipleFactoriesDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var factory = factories[0];

        // Validate factory signature
        if (!ValidateFactorySignature(factory, config.KeyType, typeSymbol, context))
            return;

        // Check for cache name conflict
        var cacheName = config.CacheTypeName ?? (typeSymbol.Name + "Cache");
        var existingMembers = typeSymbol.GetMembers().Select(m => m.Name);
        if (existingMembers.Contains(cacheName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CacheNameConflictDescriptor,
                node.GetLocation(),
                cacheName));
            return;
        }

        // Generate the cache
        var source = GenerateFlyweightCache(typeSymbol, config, factory, cacheName);
        var fileName = $"{typeSymbol.Name}.Flyweight.g.cs";
        context.AddSource(fileName, source);
    }

    private FlyweightConfig ParseFlyweightConfig(AttributeData attribute)
    {
        var config = new FlyweightConfig();

        // Parse constructor argument (keyType)
        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is INamedTypeSymbol keyType)
        {
            config.KeyType = keyType;
            config.KeyTypeDisplay = keyType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        // Parse named arguments
        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "CacheTypeName":
                    config.CacheTypeName = namedArg.Value.Value?.ToString();
                    break;
                case "Capacity":
                    config.Capacity = namedArg.Value.Value is int cap ? cap : 0;
                    break;
                case "Eviction":
                    config.Eviction = namedArg.Value.Value is int ev ? ev : 0;
                    break;
                case "Threading":
                    config.Threading = namedArg.Value.Value is int th ? th : 1;
                    break;
                case "GenerateTryGet":
                    config.GenerateTryGet = namedArg.Value.Value is bool tryGet && tryGet;
                    break;
            }
        }

        return config;
    }

    private List<IMethodSymbol> CollectFactoryMethods(INamedTypeSymbol typeSymbol)
    {
        var factories = new List<IMethodSymbol>();

        foreach (var method in typeSymbol.GetMembers().OfType<IMethodSymbol>())
        {
            var factoryAttr = method.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Flyweight.FlyweightFactoryAttribute");

            if (factoryAttr is not null)
                factories.Add(method);
        }

        return factories;
    }

    private bool ValidateFactorySignature(
        IMethodSymbol factory,
        INamedTypeSymbol? keyType,
        INamedTypeSymbol valueType,
        SourceProductionContext context)
    {
        // Must be static
        if (!factory.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidFactorySignatureDescriptor,
                factory.Locations.FirstOrDefault(),
                factory.Name));
            return false;
        }

        // Must have exactly one parameter
        if (factory.Parameters.Length != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidFactorySignatureDescriptor,
                factory.Locations.FirstOrDefault(),
                factory.Name));
            return false;
        }

        // Return type must be the value type (the annotated type)
        if (!SymbolEqualityComparer.Default.Equals(factory.ReturnType, valueType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidFactorySignatureDescriptor,
                factory.Locations.FirstOrDefault(),
                factory.Name));
            return false;
        }

        // Parameter type must match key type if specified
        if (keyType is not null &&
            !SymbolEqualityComparer.Default.Equals(factory.Parameters[0].Type, keyType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidFactorySignatureDescriptor,
                factory.Locations.FirstOrDefault(),
                factory.Name));
            return false;
        }

        return true;
    }

    private string GenerateFlyweightCache(
        INamedTypeSymbol typeSymbol,
        FlyweightConfig config,
        IMethodSymbol factory,
        string cacheName)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";
        var valueTypeDisplay = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var keyTypeDisplay = config.KeyTypeDisplay ?? factory.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var factoryName = factory.Name;

        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        // Partial type with nested cache class
        sb.AppendLine($"partial {recordKeyword}{typeKind} {typeName}");
        sb.AppendLine("{");

        // Cache class
        sb.AppendLine($"    /// <summary>Cache for {typeName} flyweight instances.</summary>");
        sb.AppendLine($"    public static class {cacheName}");
        sb.AppendLine("    {");

        // Storage based on threading policy
        if (config.Threading == 2) // Concurrent
        {
            sb.AppendLine($"        private static readonly System.Collections.Concurrent.ConcurrentDictionary<{keyTypeDisplay}, {valueTypeDisplay}> _cache = new();");
        }
        else
        {
            sb.AppendLine($"        private static readonly System.Collections.Generic.Dictionary<{keyTypeDisplay}, {valueTypeDisplay}> _cache = new();");
        }

        if (config.Threading == 1) // Locking
        {
            sb.AppendLine("        private static readonly object _lock = new();");
        }

        if (config.Eviction == 1) // LRU
        {
            sb.AppendLine($"        private static readonly System.Collections.Generic.LinkedList<{keyTypeDisplay}> _accessOrder = new();");
            sb.AppendLine($"        private static readonly System.Collections.Generic.Dictionary<{keyTypeDisplay}, System.Collections.Generic.LinkedListNode<{keyTypeDisplay}>> _nodes = new();");
        }

        sb.AppendLine();

        // Get method
        sb.AppendLine($"        /// <summary>Gets or creates a flyweight instance for the specified key.</summary>");
        sb.AppendLine($"        public static {valueTypeDisplay} Get({keyTypeDisplay} key)");
        sb.AppendLine("        {");

        if (config.Threading == 2) // Concurrent
        {
            sb.AppendLine($"            return _cache.GetOrAdd(key, static k => {typeName}.{factoryName}(k));");
        }
        else if (config.Threading == 1) // Locking
        {
            sb.AppendLine("            lock (_lock)");
            sb.AppendLine("            {");
            GenerateGetBody(sb, config, typeName, factoryName, keyTypeDisplay, valueTypeDisplay, "                ");
            sb.AppendLine("            }");
        }
        else // SingleThreadedFast
        {
            GenerateGetBody(sb, config, typeName, factoryName, keyTypeDisplay, valueTypeDisplay, "            ");
        }

        sb.AppendLine("        }");

        // TryGet method
        if (config.GenerateTryGet)
        {
            sb.AppendLine();
            sb.AppendLine($"        /// <summary>Attempts to get a cached flyweight instance without creating one.</summary>");
            sb.AppendLine($"        public static bool TryGet({keyTypeDisplay} key, out {valueTypeDisplay} value)");
            sb.AppendLine("        {");

            if (config.Threading == 2) // Concurrent
            {
                sb.AppendLine("            return _cache.TryGetValue(key, out value);");
            }
            else if (config.Threading == 1) // Locking
            {
                sb.AppendLine("            lock (_lock)");
                sb.AppendLine("            {");
                sb.AppendLine("                return _cache.TryGetValue(key, out value);");
                sb.AppendLine("            }");
            }
            else // SingleThreadedFast
            {
                sb.AppendLine("            return _cache.TryGetValue(key, out value);");
            }

            sb.AppendLine("        }");
        }

        // Clear method
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Clears all cached flyweight instances.</summary>");
        sb.AppendLine("        public static void Clear()");
        sb.AppendLine("        {");

        if (config.Threading == 1) // Locking
        {
            sb.AppendLine("            lock (_lock)");
            sb.AppendLine("            {");
            sb.AppendLine("                _cache.Clear();");
            if (config.Eviction == 1)
            {
                sb.AppendLine("                _accessOrder.Clear();");
                sb.AppendLine("                _nodes.Clear();");
            }
            sb.AppendLine("            }");
        }
        else if (config.Threading == 2) // Concurrent
        {
            sb.AppendLine("            _cache.Clear();");
        }
        else
        {
            sb.AppendLine("            _cache.Clear();");
            if (config.Eviction == 1)
            {
                sb.AppendLine("            _accessOrder.Clear();");
                sb.AppendLine("            _nodes.Clear();");
            }
        }

        sb.AppendLine("        }");

        // Count property
        sb.AppendLine();
        sb.AppendLine("        /// <summary>Gets the number of cached instances.</summary>");
        sb.AppendLine("        public static int Count");
        sb.AppendLine("        {");
        sb.AppendLine("            get");
        sb.AppendLine("            {");
        if (config.Threading == 1)
        {
            sb.AppendLine("                lock (_lock)");
            sb.AppendLine("                {");
            sb.AppendLine("                    return _cache.Count;");
            sb.AppendLine("                }");
        }
        else
        {
            sb.AppendLine("                return _cache.Count;");
        }
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateGetBody(
        StringBuilder sb,
        FlyweightConfig config,
        string typeName,
        string factoryName,
        string keyTypeDisplay,
        string valueTypeDisplay,
        string indent)
    {
        sb.AppendLine($"{indent}if (_cache.TryGetValue(key, out var existing))");
        sb.AppendLine($"{indent}{{");
        if (config.Eviction == 1) // LRU - update access order
        {
            sb.AppendLine($"{indent}    if (_nodes.TryGetValue(key, out var node))");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _accessOrder.Remove(node);");
            sb.AppendLine($"{indent}        _accessOrder.AddLast(node);");
            sb.AppendLine($"{indent}    }}");
        }
        sb.AppendLine($"{indent}    return existing;");
        sb.AppendLine($"{indent}}}");

        if (config.Capacity > 0 && config.Eviction == 1) // LRU eviction
        {
            sb.AppendLine($"{indent}if (_cache.Count >= {config.Capacity})");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    var lruKey = _accessOrder.First!.Value;");
            sb.AppendLine($"{indent}    _accessOrder.RemoveFirst();");
            sb.AppendLine($"{indent}    _nodes.Remove(lruKey);");
            sb.AppendLine($"{indent}    _cache.Remove(lruKey);");
            sb.AppendLine($"{indent}}}");
        }
        else if (config.Capacity > 0 && config.Eviction == 0) // No eviction, bounded
        {
            sb.AppendLine($"{indent}if (_cache.Count >= {config.Capacity})");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    return {typeName}.{factoryName}(key);");
            sb.AppendLine($"{indent}}}");
        }

        sb.AppendLine($"{indent}var value = {typeName}.{factoryName}(key);");
        sb.AppendLine($"{indent}_cache[key] = value;");

        if (config.Eviction == 1) // LRU - track access order
        {
            sb.AppendLine($"{indent}var newNode = _accessOrder.AddLast(key);");
            sb.AppendLine($"{indent}_nodes[key] = newNode;");
        }

        sb.AppendLine($"{indent}return value;");
    }

    // Helper classes
    private class FlyweightConfig
    {
        public INamedTypeSymbol? KeyType { get; set; }
        public string? KeyTypeDisplay { get; set; }
        public string? CacheTypeName { get; set; }
        public int Capacity { get; set; }
        public int Eviction { get; set; } // 0=None, 1=Lru
        public int Threading { get; set; } = 1; // 0=SingleThreadedFast, 1=Locking, 2=Concurrent
        public bool GenerateTryGet { get; set; } = true;
    }
}
