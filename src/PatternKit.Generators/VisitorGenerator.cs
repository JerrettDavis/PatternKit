using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator that produces GoF-compliant visitor pattern infrastructure with true double-dispatch.
/// Generates visitor interfaces, Accept methods, and fluent builders for types marked with [GenerateVisitor].
/// The generated code is self-contained with no dependencies on PatternKit.Core.
/// </summary>
[Generator]
public sealed class VisitorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types (classes, interfaces, structs, records) marked with [GenerateVisitor]
        var visitorRoots = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Visitors.GenerateVisitorAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax 
                                        or InterfaceDeclarationSyntax 
                                        or StructDeclarationSyntax 
                                        or RecordDeclarationSyntax,
            transform: static (gasc, ct) => GetVisitorRoot(gasc, ct)
        ).Where(static x => x is not null);

        // Generate visitor infrastructure for each root
        context.RegisterSourceOutput(visitorRoots.Collect(), static (spc, roots) =>
        {
            foreach (var root in roots)
            {
                if (root is null) continue;
                GenerateVisitorInfrastructure(spc, root.Value);
            }
        });
    }

    private static VisitorRootInfo? GetVisitorRoot(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol baseType)
            return null;

        var attr = context.Attributes[0];
        
        // Read attribute properties
        var visitorInterfaceName = GetAttributeProperty<string>(attr, "VisitorInterfaceName");
        var generateAsync = GetAttributeProperty<bool?>(attr, "GenerateAsync") ?? true;
        var generateActions = GetAttributeProperty<bool?>(attr, "GenerateActions") ?? true;
        var autoDiscover = GetAttributeProperty<bool?>(attr, "AutoDiscoverDerivedTypes") ?? true;

        var ns = baseType.ContainingNamespace.IsGlobalNamespace 
            ? null 
            : baseType.ContainingNamespace.ToDisplayString();

        var baseName = baseType.Name;
        var baseFullName = baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        
        // Generate visitor interface name intelligently
        // If base name starts with "I" and is an interface, don't add another "I"
        string defaultVisitorName;
        if (baseType.TypeKind == TypeKind.Interface && baseName.StartsWith("I") && baseName.Length > 1 && char.IsUpper(baseName[1]))
        {
            // Interface name like "IShape" -> "IShapeVisitor"
            defaultVisitorName = $"{baseName}Visitor";
        }
        else
        {
            // Class name like "Shape" -> "IShapeVisitor"
            defaultVisitorName = $"I{baseName}Visitor";
        }

        // Discover derived types in the same assembly
        var derivedTypes = autoDiscover 
            ? DiscoverDerivedTypes(baseType, context.SemanticModel.Compilation)
            : ImmutableArray<INamedTypeSymbol>.Empty;

        return new VisitorRootInfo(
            Namespace: ns,
            BaseName: baseName,
            BaseFullName: baseFullName,
            BaseType: baseType,
            VisitorInterfaceName: visitorInterfaceName ?? defaultVisitorName,
            GenerateAsync: generateAsync,
            GenerateActions: generateActions,
            DerivedTypes: derivedTypes
        );
    }

    private static T? GetAttributeProperty<T>(AttributeData attr, string propertyName)
    {
        var prop = attr.NamedArguments.FirstOrDefault(x => x.Key == propertyName);
        if (prop.Value.Value is T value)
            return value;
        return default;
    }

    private static ImmutableArray<INamedTypeSymbol> DiscoverDerivedTypes(
        INamedTypeSymbol baseType, 
        Compilation compilation)
    {
        var derived = new List<INamedTypeSymbol>();
        
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            
            // Discover classes
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (symbol is null || SymbolEqualityComparer.Default.Equals(symbol, baseType)) continue;
                
                if (IsDerivedFrom(symbol, baseType))
                {
                    derived.Add(symbol);
                }
            }
            
            // Discover structs
            foreach (var structDecl in root.DescendantNodes().OfType<StructDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(structDecl) as INamedTypeSymbol;
                if (symbol is null || SymbolEqualityComparer.Default.Equals(symbol, baseType)) continue;
                
                if (ImplementsInterface(symbol, baseType))
                {
                    derived.Add(symbol);
                }
            }
            
            // Discover records
            foreach (var recordDecl in root.DescendantNodes().OfType<RecordDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(recordDecl) as INamedTypeSymbol;
                if (symbol is null || SymbolEqualityComparer.Default.Equals(symbol, baseType)) continue;
                
                if (IsDerivedFrom(symbol, baseType))
                {
                    derived.Add(symbol);
                }
            }
        }
        
        return derived.ToImmutableArray();
    }
    
    private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        // Check class inheritance
        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        
        // Check interface implementation
        return ImplementsInterface(type, baseType);
    }
    
    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (interfaceType.TypeKind != TypeKind.Interface)
            return false;
            
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceType))
                return true;
        }
        
        return false;
    }

    private static void GenerateVisitorInfrastructure(SourceProductionContext context, VisitorRootInfo root)
    {
        // Generate visitor interfaces
        GenerateVisitorInterfaces(context, root);
        
        // Generate Accept methods for base and derived types
        GenerateAcceptMethods(context, root);
        
        // Generate fluent builders
        GenerateFluentBuilders(context, root);
    }

    private static void GenerateVisitorInterfaces(SourceProductionContext context, VisitorRootInfo root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (root.Namespace is not null)
        {
            sb.AppendLine($"namespace {root.Namespace};");
            sb.AppendLine();
        }

        var visitableTypes = GetAllVisitableTypes(root);

        // Generate sync result visitor
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Visitor interface for {root.BaseName} hierarchy that returns a result.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public interface {root.VisitorInterfaceName}<TResult>");
        sb.AppendLine("{");
        foreach (var type in visitableTypes)
        {
            var typeName = type.Name;
            var paramName = ToCamelCase(typeName);
            sb.AppendLine($"    TResult Visit({typeName} {paramName});");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate action visitor if requested
        if (root.GenerateActions)
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Action visitor interface for {root.BaseName} hierarchy (no return value).");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public interface {root.VisitorInterfaceName}Action");
            sb.AppendLine("{");
            foreach (var type in visitableTypes)
            {
                var typeName = type.Name;
                var paramName = ToCamelCase(typeName);
                sb.AppendLine($"    void Visit({typeName} {paramName});");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Generate async visitor if requested
        if (root.GenerateAsync)
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Async visitor interface for {root.BaseName} hierarchy that returns a ValueTask result.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"public interface {root.VisitorInterfaceName}Async<TResult>");
            sb.AppendLine("{");
            foreach (var type in visitableTypes)
            {
                var typeName = type.Name;
                var paramName = ToCamelCase(typeName);
                sb.AppendLine($"    System.Threading.Tasks.ValueTask<TResult> VisitAsync({typeName} {paramName}, System.Threading.CancellationToken cancellationToken = default);");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            if (root.GenerateActions)
            {
                sb.AppendLine("/// <summary>");
                sb.AppendLine($"/// Async action visitor interface for {root.BaseName} hierarchy (no return value).");
                sb.AppendLine("/// </summary>");
                sb.AppendLine($"public interface {root.VisitorInterfaceName}AsyncAction");
                sb.AppendLine("{");
                foreach (var type in visitableTypes)
                {
                    var typeName = type.Name;
                    var paramName = ToCamelCase(typeName);
                    sb.AppendLine($"    System.Threading.Tasks.ValueTask VisitAsync({typeName} {paramName}, System.Threading.CancellationToken cancellationToken = default);");
                }
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        context.AddSource($"{root.VisitorInterfaceName}.Interfaces.g.cs", sb.ToString());
    }

    private static void GenerateAcceptMethods(SourceProductionContext context, VisitorRootInfo root)
    {
        var allTypes = GetAllVisitableTypes(root);
        
        foreach (var type in allTypes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#nullable enable");
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine();

            var ns = type.ContainingNamespace.IsGlobalNamespace 
                ? null 
                : type.ContainingNamespace.ToDisplayString();

            if (ns is not null)
            {
                sb.AppendLine($"namespace {ns};");
                sb.AppendLine();
            }

            // Determine the type keyword (class, struct, interface, record)
            string typeKeyword = type.TypeKind switch
            {
                TypeKind.Interface => "interface",
                TypeKind.Struct => "struct",
                _ => "class"
            };

            sb.AppendLine($"public partial {typeKeyword} {type.Name}");
            sb.AppendLine("{");

            // Sync Accept with result
            sb.AppendLine($"    /// <summary>Accepts a visitor and returns a result.</summary>");
            sb.AppendLine($"    public TResult Accept<TResult>({root.VisitorInterfaceName}<TResult> visitor)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return visitor.Visit(this);");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Action Accept if requested
            if (root.GenerateActions)
            {
                sb.AppendLine($"    /// <summary>Accepts an action visitor.</summary>");
                sb.AppendLine($"    public void Accept({root.VisitorInterfaceName}Action visitor)");
                sb.AppendLine("    {");
                sb.AppendLine($"        visitor.Visit(this);");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            // Async Accept if requested
            if (root.GenerateAsync)
            {
                sb.AppendLine($"    /// <summary>Accepts an async visitor and returns a ValueTask result.</summary>");
                sb.AppendLine($"    public System.Threading.Tasks.ValueTask<TResult> AcceptAsync<TResult>({root.VisitorInterfaceName}Async<TResult> visitor, System.Threading.CancellationToken cancellationToken = default)");
                sb.AppendLine("    {");
                sb.AppendLine($"        return visitor.VisitAsync(this, cancellationToken);");
                sb.AppendLine("    }");
                sb.AppendLine();

                if (root.GenerateActions)
                {
                    sb.AppendLine($"    /// <summary>Accepts an async action visitor.</summary>");
                    sb.AppendLine($"    public System.Threading.Tasks.ValueTask AcceptAsync({root.VisitorInterfaceName}AsyncAction visitor, System.Threading.CancellationToken cancellationToken = default)");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        return visitor.VisitAsync(this, cancellationToken);");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("}");

            context.AddSource($"{type.Name}.Accept.g.cs", sb.ToString());
        }
    }

    private static void GenerateFluentBuilders(SourceProductionContext context, VisitorRootInfo root)
    {
        var allTypes = GetAllVisitableTypes(root);
        
        // Generate sync result builder
        GenerateSyncResultBuilder(context, root, allTypes);
        
        if (root.GenerateActions)
        {
            GenerateSyncActionBuilder(context, root, allTypes);
        }
        
        if (root.GenerateAsync)
        {
            GenerateAsyncResultBuilder(context, root, allTypes);
            
            if (root.GenerateActions)
            {
                GenerateAsyncActionBuilder(context, root, allTypes);
            }
        }
    }

    private static void GenerateSyncResultBuilder(
        SourceProductionContext context, 
        VisitorRootInfo root, 
        ImmutableArray<INamedTypeSymbol> visitableTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (root.Namespace is not null)
        {
            sb.AppendLine($"namespace {root.Namespace};");
            sb.AppendLine();
        }

        var builderName = $"{root.BaseName}VisitorBuilder";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Fluent builder for creating {root.VisitorInterfaceName} implementations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {builderName}<TResult>");
        sb.AppendLine("{");
        
        // Store handlers in a dictionary keyed by Type
        sb.AppendLine($"    private readonly System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, TResult>> _handlers = new();");
        sb.AppendLine($"    private System.Func<{root.BaseName}, TResult>? _defaultHandler;");
        sb.AppendLine();

        // Generate generic When<T> method
        sb.AppendLine($"    /// <summary>Registers a handler for nodes of type <typeparamref name=\"T\"/>.</summary>");
        sb.AppendLine($"    /// <typeparam name=\"T\">A concrete type assignable to {root.BaseName}.</typeparam>");
        sb.AppendLine($"    /// <param name=\"handler\">The handler invoked when the runtime type is <typeparamref name=\"T\"/>.</param>");
        sb.AppendLine($"    public {builderName}<TResult> When<T>(System.Func<T, TResult> handler) where T : {root.BaseName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        _handlers[typeof(T)] = node => handler((T)node);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Default handler
        sb.AppendLine($"    /// <summary>Sets a default handler for unmatched types.</summary>");
        sb.AppendLine($"    public {builderName}<TResult> Default(System.Func<{root.BaseName}, TResult> handler)");
        sb.AppendLine("    {");
        sb.AppendLine("        _defaultHandler = handler;");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Build method
        sb.AppendLine($"    /// <summary>Builds the visitor implementation.</summary>");
        sb.AppendLine($"    public {root.VisitorInterfaceName}<TResult> Build()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new Implementation(_handlers, _defaultHandler);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Implementation class
        sb.AppendLine($"    private sealed class Implementation : {root.VisitorInterfaceName}<TResult>");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, TResult>> _handlers;");
        sb.AppendLine($"        private readonly System.Func<{root.BaseName}, TResult>? _defaultHandler;");
        sb.AppendLine();
        
        sb.AppendLine($"        internal Implementation(System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, TResult>> handlers, System.Func<{root.BaseName}, TResult>? defaultHandler)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _handlers = handlers;");
        sb.AppendLine("            _defaultHandler = defaultHandler;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Visit methods
        foreach (var type in visitableTypes)
        {
            var typeName = type.Name;
            var paramName = ToCamelCase(typeName);
            
            sb.AppendLine($"        public TResult Visit({typeName} {paramName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (_handlers.TryGetValue(typeof({typeName}), out var handler))");
            sb.AppendLine($"                return handler({paramName});");
            sb.AppendLine($"            if (_defaultHandler is not null)");
            sb.AppendLine($"                return _defaultHandler({paramName});");
            sb.AppendLine($"            throw new System.InvalidOperationException($\"No handler registered for type {typeName}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"{builderName}.g.cs", sb.ToString());
    }

    private static void GenerateSyncActionBuilder(
        SourceProductionContext context, 
        VisitorRootInfo root, 
        ImmutableArray<INamedTypeSymbol> visitableTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (root.Namespace is not null)
        {
            sb.AppendLine($"namespace {root.Namespace};");
            sb.AppendLine();
        }

        var builderName = $"{root.BaseName}ActionVisitorBuilder";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Fluent builder for creating {root.VisitorInterfaceName}Action implementations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {builderName}");
        sb.AppendLine("{");
        
        sb.AppendLine($"    private readonly System.Collections.Generic.Dictionary<System.Type, System.Action<{root.BaseName}>> _handlers = new();");
        sb.AppendLine($"    private System.Action<{root.BaseName}>? _defaultHandler;");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Registers an action handler for nodes of type <typeparamref name=\"T\"/>.</summary>");
        sb.AppendLine($"    /// <typeparam name=\"T\">A concrete type assignable to {root.BaseName}.</typeparam>");
        sb.AppendLine($"    /// <param name=\"handler\">The action invoked when the runtime type is <typeparamref name=\"T\"/>.</param>");
        sb.AppendLine($"    public {builderName} When<T>(System.Action<T> handler) where T : {root.BaseName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        _handlers[typeof(T)] = node => handler((T)node);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Sets a default handler for unmatched types.</summary>");
        sb.AppendLine($"    public {builderName} Default(System.Action<{root.BaseName}> handler)");
        sb.AppendLine("    {");
        sb.AppendLine("        _defaultHandler = handler;");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Builds the visitor implementation.</summary>");
        sb.AppendLine($"    public {root.VisitorInterfaceName}Action Build()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new Implementation(_handlers, _defaultHandler);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    private sealed class Implementation : {root.VisitorInterfaceName}Action");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly System.Collections.Generic.Dictionary<System.Type, System.Action<{root.BaseName}>> _handlers;");
        sb.AppendLine($"        private readonly System.Action<{root.BaseName}>? _defaultHandler;");
        sb.AppendLine();
        
        sb.AppendLine($"        internal Implementation(System.Collections.Generic.Dictionary<System.Type, System.Action<{root.BaseName}>> handlers, System.Action<{root.BaseName}>? defaultHandler)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _handlers = handlers;");
        sb.AppendLine("            _defaultHandler = defaultHandler;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var type in visitableTypes)
        {
            var typeName = type.Name;
            var paramName = ToCamelCase(typeName);
            
            sb.AppendLine($"        public void Visit({typeName} {paramName})");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (_handlers.TryGetValue(typeof({typeName}), out var handler))");
            sb.AppendLine($"                handler({paramName});");
            sb.AppendLine($"            else if (_defaultHandler is not null)");
            sb.AppendLine($"                _defaultHandler({paramName});");
            sb.AppendLine($"            else");
            sb.AppendLine($"                throw new System.InvalidOperationException($\"No handler registered for type {typeName}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"{builderName}.g.cs", sb.ToString());
    }

    private static void GenerateAsyncResultBuilder(
        SourceProductionContext context, 
        VisitorRootInfo root, 
        ImmutableArray<INamedTypeSymbol> visitableTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (root.Namespace is not null)
        {
            sb.AppendLine($"namespace {root.Namespace};");
            sb.AppendLine();
        }

        var builderName = $"{root.BaseName}AsyncVisitorBuilder";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Fluent builder for creating {root.VisitorInterfaceName}Async implementations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {builderName}<TResult>");
        sb.AppendLine("{");
        
        sb.AppendLine($"    private readonly System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>>> _handlers = new();");
        sb.AppendLine($"    private System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>>? _defaultHandler;");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Registers an async handler for nodes of type <typeparamref name=\"T\"/>.</summary>");
        sb.AppendLine($"    /// <typeparam name=\"T\">A concrete type assignable to {root.BaseName}.</typeparam>");
        sb.AppendLine($"    /// <param name=\"handler\">The async handler invoked when the runtime type is <typeparamref name=\"T\"/>.</param>");
        sb.AppendLine($"    public {builderName}<TResult> WhenAsync<T>(System.Func<T, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>> handler) where T : {root.BaseName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        _handlers[typeof(T)] = (node, ct) => handler((T)node, ct);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Sets a default async handler for unmatched types.</summary>");
        sb.AppendLine($"    public {builderName}<TResult> DefaultAsync(System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>> handler)");
        sb.AppendLine("    {");
        sb.AppendLine("        _defaultHandler = handler;");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Builds the async visitor implementation.</summary>");
        sb.AppendLine($"    public {root.VisitorInterfaceName}Async<TResult> Build()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new Implementation(_handlers, _defaultHandler);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    private sealed class Implementation : {root.VisitorInterfaceName}Async<TResult>");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>>> _handlers;");
        sb.AppendLine($"        private readonly System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>>? _defaultHandler;");
        sb.AppendLine();
        
        sb.AppendLine($"        internal Implementation(System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>>> handlers, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<TResult>>? defaultHandler)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _handlers = handlers;");
        sb.AppendLine("            _defaultHandler = defaultHandler;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var type in visitableTypes)
        {
            var typeName = type.Name;
            var paramName = ToCamelCase(typeName);
            
            sb.AppendLine($"        public System.Threading.Tasks.ValueTask<TResult> VisitAsync({typeName} {paramName}, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (_handlers.TryGetValue(typeof({typeName}), out var handler))");
            sb.AppendLine($"                return handler({paramName}, cancellationToken);");
            sb.AppendLine($"            if (_defaultHandler is not null)");
            sb.AppendLine($"                return _defaultHandler({paramName}, cancellationToken);");
            sb.AppendLine($"            throw new System.InvalidOperationException($\"No handler registered for type {typeName}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"{builderName}.g.cs", sb.ToString());
    }

    private static void GenerateAsyncActionBuilder(
        SourceProductionContext context, 
        VisitorRootInfo root, 
        ImmutableArray<INamedTypeSymbol> visitableTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();

        if (root.Namespace is not null)
        {
            sb.AppendLine($"namespace {root.Namespace};");
            sb.AppendLine();
        }

        var builderName = $"{root.BaseName}AsyncActionVisitorBuilder";

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Fluent builder for creating {root.VisitorInterfaceName}AsyncAction implementations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {builderName}");
        sb.AppendLine("{");
        
        sb.AppendLine($"    private readonly System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask>> _handlers = new();");
        sb.AppendLine($"    private System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask>? _defaultHandler;");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Registers an async action handler for nodes of type <typeparamref name=\"T\"/>.</summary>");
        sb.AppendLine($"    /// <typeparam name=\"T\">A concrete type assignable to {root.BaseName}.</typeparam>");
        sb.AppendLine($"    /// <param name=\"handler\">The async action invoked when the runtime type is <typeparamref name=\"T\"/>.</param>");
        sb.AppendLine($"    public {builderName} WhenAsync<T>(System.Func<T, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> handler) where T : {root.BaseName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        _handlers[typeof(T)] = (node, ct) => handler((T)node, ct);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Sets a default async action handler for unmatched types.</summary>");
        sb.AppendLine($"    public {builderName} DefaultAsync(System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask> handler)");
        sb.AppendLine("    {");
        sb.AppendLine("        _defaultHandler = handler;");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Builds the async action visitor implementation.</summary>");
        sb.AppendLine($"    public {root.VisitorInterfaceName}AsyncAction Build()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new Implementation(_handlers, _defaultHandler);");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    private sealed class Implementation : {root.VisitorInterfaceName}AsyncAction");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask>> _handlers;");
        sb.AppendLine($"        private readonly System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask>? _defaultHandler;");
        sb.AppendLine();
        
        sb.AppendLine($"        internal Implementation(System.Collections.Generic.Dictionary<System.Type, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask>> handlers, System.Func<{root.BaseName}, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask>? defaultHandler)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _handlers = handlers;");
        sb.AppendLine("            _defaultHandler = defaultHandler;");
        sb.AppendLine("        }");
        sb.AppendLine();

        foreach (var type in visitableTypes)
        {
            var typeName = type.Name;
            var paramName = ToCamelCase(typeName);
            
            sb.AppendLine($"        public System.Threading.Tasks.ValueTask VisitAsync({typeName} {paramName}, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (_handlers.TryGetValue(typeof({typeName}), out var handler))");
            sb.AppendLine($"                return handler({paramName}, cancellationToken);");
            sb.AppendLine($"            if (_defaultHandler is not null)");
            sb.AppendLine($"                return _defaultHandler({paramName}, cancellationToken);");
            sb.AppendLine($"            throw new System.InvalidOperationException($\"No handler registered for type {typeName}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"{builderName}.g.cs", sb.ToString());
    }

    private static ImmutableArray<INamedTypeSymbol> GetAllVisitableTypes(VisitorRootInfo root)
    {
        // Include base type and all derived types
        var result = new List<INamedTypeSymbol> { root.BaseType };
        result.AddRange(root.DerivedTypes);
        return result.ToImmutableArray();
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length == 1) 
            return name.ToLowerInvariant();
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private readonly record struct VisitorRootInfo(
        string? Namespace,
        string BaseName,
        string BaseFullName,
        INamedTypeSymbol BaseType,
        string VisitorInterfaceName,
        bool GenerateAsync,
        bool GenerateActions,
        ImmutableArray<INamedTypeSymbol> DerivedTypes
    );
}
