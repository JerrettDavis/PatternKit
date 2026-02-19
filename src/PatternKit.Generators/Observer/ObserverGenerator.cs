using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PatternKit.Generators.Observer;

/// <summary>
/// Incremental source generator for the Observer pattern.
/// Generates Subscribe/Publish methods with configurable threading, exception, and ordering policies.
/// </summary>
[Generator]
public sealed class ObserverGenerator : IIncrementalGenerator
{
    // Diagnostic IDs
    private const string DiagnosticIdNotPartial = "PKOBS001";
    private const string DiagnosticIdHubNotPartialStatic = "PKOBS002";
    private const string DiagnosticIdInvalidEventProperty = "PKOBS003";
    private const string DiagnosticIdAsyncUnsupported = "PKOBS004";
    private const string DiagnosticIdInvalidConfig = "PKOBS005";

    // Diagnostic descriptors
    private static readonly DiagnosticDescriptor NotPartialRule = new(
        DiagnosticIdNotPartial,
        "Type must be partial",
        "Type '{0}' marked with [Observer] must be declared as partial",
        "PatternKit.Generators.Observer",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HubNotPartialStaticRule = new(
        DiagnosticIdHubNotPartialStatic,
        "Hub type must be partial and static",
        "Type '{0}' marked with [ObserverHub] must be declared as partial static",
        "PatternKit.Generators.Observer",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidEventPropertyRule = new(
        DiagnosticIdInvalidEventProperty,
        "Invalid event property",
        "Property '{0}' marked with [ObservedEvent] must be static partial with a getter only",
        "PatternKit.Generators.Observer",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncUnsupportedRule = new(
        DiagnosticIdAsyncUnsupported,
        "Async not supported",
        "Async publish requested but async handler shape is unsupported in this context",
        "PatternKit.Generators.Observer",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidConfigRule = new(
        DiagnosticIdInvalidConfig,
        "Invalid configuration",
        "{0}",
        "PatternKit.Generators.Observer",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all types marked with [Observer]
        var observerTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Observer.ObserverAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate observer implementations
        context.RegisterSourceOutput(observerTypes, static (spc, occ) =>
        {
            GenerateObserver(spc, occ);
        });

        // Find all types marked with [ObserverHub]
        var hubTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Observer.ObserverHubAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        // Generate hub implementations
        context.RegisterSourceOutput(hubTypes, static (spc, occ) =>
        {
            GenerateHub(spc, occ);
        });
    }

    private static void GenerateObserver(SourceProductionContext context, GeneratorAttributeSyntaxContext occurrence)
    {
        var typeSymbol = (INamedTypeSymbol)occurrence.TargetSymbol;
        var syntax = (TypeDeclarationSyntax)occurrence.TargetNode;

        // Validate that type is partial
        if (!IsPartial(syntax))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotPartialRule,
                syntax.Identifier.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Extract configuration from attribute
        var config = ExtractObserverConfig(occurrence.Attributes[0]);

        // Validate configuration
        if (!ValidateConfiguration(config, context, syntax.GetLocation()))
        {
            return;
        }

        // Generate the source code
        var source = GenerateObserverSource(typeSymbol, config);
        var fileName = $"{typeSymbol.Name}.Observer.g.cs";
        context.AddSource(fileName, source);
    }

    private static void GenerateHub(SourceProductionContext context, GeneratorAttributeSyntaxContext occurrence)
    {
        var typeSymbol = (INamedTypeSymbol)occurrence.TargetSymbol;
        var syntax = (ClassDeclarationSyntax)occurrence.TargetNode;

        // Validate that type is partial and static
        if (!IsPartial(syntax) || !IsStatic(syntax))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HubNotPartialStaticRule,
                syntax.Identifier.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Find all properties marked with [ObservedEvent]
        var eventProperties = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Observer.ObservedEventAttribute"))
            .ToList();

        // Validate event properties
        foreach (var prop in eventProperties)
        {
            if (!prop.IsStatic || prop.SetMethod != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidEventPropertyRule,
                    prop.Locations.FirstOrDefault() ?? Location.None,
                    prop.Name));
                return;
            }
        }

        // Generate the hub source code
        var source = GenerateHubSource(typeSymbol, eventProperties);
        var fileName = $"{typeSymbol.Name}.ObserverHub.g.cs";
        context.AddSource(fileName, source);
    }

    private static bool IsPartial(TypeDeclarationSyntax syntax)
    {
        return syntax.Modifiers.Any(m => m.Text == "partial");
    }

    private static bool IsStatic(TypeDeclarationSyntax syntax)
    {
        return syntax.Modifiers.Any(m => m.Text == "static");
    }

    private static ObserverConfig ExtractObserverConfig(AttributeData attribute)
    {
        var config = new ObserverConfig();

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Threading":
                    config.Threading = (int)namedArg.Value.Value!;
                    break;
                case "Exceptions":
                    config.Exceptions = (int)namedArg.Value.Value!;
                    break;
                case "Order":
                    config.Order = (int)namedArg.Value.Value!;
                    break;
                case "GenerateAsync":
                    config.GenerateAsync = (bool)namedArg.Value.Value!;
                    break;
                case "ForceAsync":
                    config.ForceAsync = (bool)namedArg.Value.Value!;
                    break;
            }
        }

        return config;
    }

    private static bool ValidateConfiguration(ObserverConfig config, SourceProductionContext context, Location location)
    {
        // Validate: Concurrent + RegistrationOrder requires extra work
        if (config.Threading == 2 && config.Order == 0) // Concurrent + RegistrationOrder
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidConfigRule,
                location,
                "Concurrent threading with RegistrationOrder requires additional ordering guarantees and may impact performance"));
            // This is a warning, not an error, so we continue
        }

        return true;
    }

    private static string GenerateObserverSource(INamedTypeSymbol typeSymbol, ObserverConfig config)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeKind = GetTypeKind(typeSymbol);
        var typeName = typeSymbol.Name;

        var code = new CodeBuilder();

        code.AppendLine("#nullable enable");
        code.AppendLine("// <auto-generated />");
        code.AppendLine();

        if (ns != null)
        {
            code.AppendLine($"namespace {ns};");
            code.AppendLine();
        }

        // Start type declaration
        code.Append($"{GetAccessibility(typeSymbol)} partial {typeKind} {typeName}");
        code.AppendLine();
        code.AppendLine("{");

        // Generate the implementation based on configuration
        GenerateObserverImplementation(code, config);

        code.AppendLine("}");

        return code.ToString();
    }

    private static void GenerateObserverImplementation(CodeBuilder code, ObserverConfig config)
    {
        // Generate subscription storage based on threading policy
        GenerateStorage(code, config);
        code.AppendLine();

        // Generate Subscribe methods
        GenerateSubscribeMethods(code, config);
        code.AppendLine();

        // Generate Publish methods
        GeneratePublishMethods(code, config);
        code.AppendLine();

        // Generate Subscription class (IDisposable)
        GenerateSubscriptionClass(code, config);
    }

    private static void GenerateStorage(CodeBuilder code, ObserverConfig config)
    {
        code.Indent();

        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                code.AppendLine("private System.Collections.Generic.List<Subscription> _subscriptions = new();");
                code.AppendLine("private int _nextId;");
                break;

            case 1: // Locking
                code.AppendLine("private readonly object _lock = new();");
                code.AppendLine("private System.Collections.Generic.List<Subscription> _subscriptions = new();");
                code.AppendLine("private int _nextId;");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    // Use ImmutableList for order preservation with concurrent access
                    code.AppendLine("private System.Collections.Immutable.ImmutableList<Subscription> _subscriptions = System.Collections.Immutable.ImmutableList<Subscription>.Empty;");
                    code.AppendLine("private int _nextId;");
                }
                else
                {
                    // Can use ConcurrentBag for better performance
                    code.AppendLine("private System.Collections.Concurrent.ConcurrentBag<Subscription> _subscriptions = new();");
                    code.AppendLine("private int _nextId;");
                }
                break;
        }

        code.Unindent();
    }

    private static void GenerateSubscribeMethods(CodeBuilder code, ObserverConfig config)
    {
        code.Indent();

        // Determine generic parameter (we'll use object for now, but ideally would infer from context)
        var eventType = "TEvent";

        if (!config.ForceAsync)
        {
            // Generate sync Subscribe method
            code.AppendLine($"public System.IDisposable Subscribe<{eventType}>(System.Action<{eventType}> handler)");
            code.AppendLine("{");
            code.Indent();
            GenerateSubscribeBody(code, config, false);
            code.Unindent();
            code.AppendLine("}");
            code.AppendLine();
        }

        if (config.GenerateAsync)
        {
            // Generate async Subscribe method
            code.AppendLine($"public System.IDisposable Subscribe<{eventType}>(System.Func<{eventType}, System.Threading.Tasks.ValueTask> handler)");
            code.AppendLine("{");
            code.Indent();
            GenerateSubscribeBody(code, config, true);
            code.Unindent();
            code.AppendLine("}");
        }

        code.Unindent();
    }

    private static void GenerateSubscribeBody(CodeBuilder code, ObserverConfig config, bool isAsync)
    {
        code.AppendLine("var id = System.Threading.Interlocked.Increment(ref _nextId);");
        code.AppendLine($"var sub = new Subscription(this, id, handler, {(isAsync ? "true" : "false")});");
        code.AppendLine();

        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                code.AppendLine("_subscriptions.Add(sub);");
                break;

            case 1: // Locking
                code.AppendLine("lock (_lock)");
                code.AppendLine("{");
                code.Indent();
                code.AppendLine("_subscriptions.Add(sub);");
                code.Unindent();
                code.AppendLine("}");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    code.AppendLine("System.Collections.Immutable.ImmutableInterlocked.Update(ref _subscriptions, static (list, s) => list.Add(s), sub);");
                }
                else
                {
                    code.AppendLine("_subscriptions.Add(sub);");
                }
                break;
        }

        code.AppendLine();
        code.AppendLine("return sub;");
    }

    private static void GeneratePublishMethods(CodeBuilder code, ObserverConfig config)
    {
        code.Indent();

        var eventType = "TEvent";

        if (!config.ForceAsync)
        {
            // Generate sync Publish method
            code.AppendLine($"public void Publish<{eventType}>({eventType} value)");
            code.AppendLine("{");
            code.Indent();
            GenerateSyncPublishBody(code, config);
            code.Unindent();
            code.AppendLine("}");
            code.AppendLine();
        }

        if (config.GenerateAsync)
        {
            // Generate async Publish method
            code.AppendLine($"public async System.Threading.Tasks.ValueTask PublishAsync<{eventType}>({eventType} value, System.Threading.CancellationToken cancellationToken = default)");
            code.AppendLine("{");
            code.Indent();
            GenerateAsyncPublishBody(code, config);
            code.Unindent();
            code.AppendLine("}");
        }

        code.Unindent();
    }

    private static void GenerateSyncPublishBody(CodeBuilder code, ObserverConfig config)
    {
        // Take snapshot based on threading policy
        GenerateSnapshot(code, config);
        code.AppendLine();

        // Generate exception handling setup
        if (config.Exceptions == 2) // Aggregate
        {
            code.AppendLine("System.Collections.Generic.List<System.Exception>? errors = null;");
            code.AppendLine();
        }

        // Iterate through snapshot
        code.AppendLine("foreach (var sub in snapshot)");
        code.AppendLine("{");
        code.Indent();

        code.AppendLine("if (sub.IsAsync) continue; // Skip async handlers in sync publish");
        code.AppendLine();

        // Try-catch based on exception policy
        if (config.Exceptions == 1) // Stop
        {
            code.AppendLine("sub.InvokeSync(value);");
        }
        else
        {
            code.AppendLine("try");
            code.AppendLine("{");
            code.Indent();
            code.AppendLine("sub.InvokeSync(value);");
            code.Unindent();
            code.AppendLine("}");
            code.AppendLine("catch (System.Exception ex)");
            code.AppendLine("{");
            code.Indent();

            if (config.Exceptions == 0) // Continue
            {
                code.AppendLine("// Optionally call OnSubscriberError if defined");
                code.AppendLine("OnSubscriberError(ex);");
            }
            else if (config.Exceptions == 2) // Aggregate
            {
                code.AppendLine("(errors ??= new()).Add(ex);");
            }

            code.Unindent();
            code.AppendLine("}");
        }

        code.Unindent();
        code.AppendLine("}");

        // Throw aggregate if needed
        if (config.Exceptions == 2)
        {
            code.AppendLine();
            code.AppendLine("if (errors is { Count: > 0 })");
            code.AppendLine("{");
            code.Indent();
            code.AppendLine("throw new System.AggregateException(errors);");
            code.Unindent();
            code.AppendLine("}");
        }
    }

    private static void GenerateAsyncPublishBody(CodeBuilder code, ObserverConfig config)
    {
        // Take snapshot
        GenerateSnapshot(code, config);
        code.AppendLine();

        // Generate exception handling setup
        if (config.Exceptions == 2) // Aggregate
        {
            code.AppendLine("System.Collections.Generic.List<System.Exception>? errors = null;");
            code.AppendLine();
        }

        // Iterate through snapshot
        code.AppendLine("foreach (var sub in snapshot)");
        code.AppendLine("{");
        code.Indent();

        // Check cancellation
        code.AppendLine("if (cancellationToken.IsCancellationRequested) break;");
        code.AppendLine();

        // Try-catch based on exception policy
        if (config.Exceptions == 1) // Stop
        {
            code.AppendLine("await sub.InvokeAsync(value, cancellationToken).ConfigureAwait(false);");
        }
        else
        {
            code.AppendLine("try");
            code.AppendLine("{");
            code.Indent();
            code.AppendLine("await sub.InvokeAsync(value, cancellationToken).ConfigureAwait(false);");
            code.Unindent();
            code.AppendLine("}");
            code.AppendLine("catch (System.Exception ex)");
            code.AppendLine("{");
            code.Indent();

            if (config.Exceptions == 0) // Continue
            {
                code.AppendLine("OnSubscriberError(ex);");
            }
            else if (config.Exceptions == 2) // Aggregate
            {
                code.AppendLine("(errors ??= new()).Add(ex);");
            }

            code.Unindent();
            code.AppendLine("}");
        }

        code.Unindent();
        code.AppendLine("}");

        // Throw aggregate if needed
        if (config.Exceptions == 2)
        {
            code.AppendLine();
            code.AppendLine("if (errors is { Count: > 0 })");
            code.AppendLine("{");
            code.Indent();
            code.AppendLine("throw new System.AggregateException(errors);");
            code.Unindent();
            code.AppendLine("}");
        }
    }

    private static void GenerateSnapshot(CodeBuilder code, ObserverConfig config)
    {
        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                code.AppendLine("var snapshot = _subscriptions.ToArray();");
                break;

            case 1: // Locking
                code.AppendLine("Subscription[] snapshot;");
                code.AppendLine("lock (_lock)");
                code.AppendLine("{");
                code.Indent();
                code.AppendLine("snapshot = _subscriptions.ToArray();");
                code.Unindent();
                code.AppendLine("}");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    code.AppendLine("var snapshot = System.Threading.Volatile.Read(ref _subscriptions).ToArray();");
                }
                else
                {
                    code.AppendLine("var snapshot = _subscriptions.ToArray();");
                }
                break;
        }
    }

    private static void GenerateSubscriptionClass(CodeBuilder code, ObserverConfig config)
    {
        code.Indent();

        code.AppendLine("partial void OnSubscriberError(System.Exception ex) { }");
        code.AppendLine();

        code.AppendLine("private void Unsubscribe(int id)");
        code.AppendLine("{");
        code.Indent();

        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                code.AppendLine("_subscriptions.RemoveAll(s => s.Id == id);");
                break;

            case 1: // Locking
                code.AppendLine("lock (_lock)");
                code.AppendLine("{");
                code.Indent();
                code.AppendLine("_subscriptions.RemoveAll(s => s.Id == id);");
                code.Unindent();
                code.AppendLine("}");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    code.AppendLine("System.Collections.Immutable.ImmutableInterlocked.Update(ref _subscriptions, static (list, id) => list.RemoveAll(s => s.Id == id), id);");
                }
                else
                {
                    code.AppendLine("// Note: ConcurrentBag doesn't support removal efficiently");
                    code.AppendLine("// Mark as removed instead");
                }
                break;
        }

        code.Unindent();
        code.AppendLine("}");
        code.AppendLine();

        // Generate Subscription nested class
        code.AppendLine("private sealed class Subscription : System.IDisposable");
        code.AppendLine("{");
        code.Indent();

        code.AppendLine("private readonly object _parent;");
        code.AppendLine("private readonly int _id;");
        code.AppendLine("private readonly object _handler;");
        code.AppendLine("private readonly bool _isAsync;");
        code.AppendLine("private int _disposed;");
        code.AppendLine();

        code.AppendLine("public int Id => _id;");
        code.AppendLine("public bool IsAsync => _isAsync;");
        code.AppendLine();

        code.AppendLine("public Subscription(object parent, int id, object handler, bool isAsync)");
        code.AppendLine("{");
        code.Indent();
        code.AppendLine("_parent = parent;");
        code.AppendLine("_id = id;");
        code.AppendLine("_handler = handler;");
        code.AppendLine("_isAsync = isAsync;");
        code.Unindent();
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("public void InvokeSync<TEvent>(TEvent value)");
        code.AppendLine("{");
        code.Indent();
        code.AppendLine("if (System.Threading.Volatile.Read(ref _disposed) != 0) return;");
        code.AppendLine("((System.Action<TEvent>)_handler)(value);");
        code.Unindent();
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("public System.Threading.Tasks.ValueTask InvokeAsync<TEvent>(TEvent value, System.Threading.CancellationToken ct)");
        code.AppendLine("{");
        code.Indent();
        code.AppendLine("if (System.Threading.Volatile.Read(ref _disposed) != 0) return default;");
        code.AppendLine("if (_isAsync)");
        code.AppendLine("{");
        code.Indent();
        code.AppendLine("return ((System.Func<TEvent, System.Threading.Tasks.ValueTask>)_handler)(value);");
        code.Unindent();
        code.AppendLine("}");
        code.AppendLine("else");
        code.AppendLine("{");
        code.Indent();
        code.AppendLine("((System.Action<TEvent>)_handler)(value);");
        code.AppendLine("return default;");
        code.Unindent();
        code.AppendLine("}");
        code.Unindent();
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("public void Dispose()");
        code.AppendLine("{");
        code.Indent();
        code.AppendLine("if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;");
        code.AppendLine("((dynamic)_parent).Unsubscribe(_id);");
        code.Unindent();
        code.AppendLine("}");

        code.Unindent();
        code.AppendLine("}");

        code.Unindent();
    }

    private static string GenerateHubSource(INamedTypeSymbol typeSymbol, List<IPropertySymbol> eventProperties)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;

        var code = new CodeBuilder();

        code.AppendLine("#nullable enable");
        code.AppendLine("// <auto-generated />");
        code.AppendLine();

        if (ns != null)
        {
            code.AppendLine($"namespace {ns};");
            code.AppendLine();
        }

        code.AppendLine($"{GetAccessibility(typeSymbol)} static partial class {typeName}");
        code.AppendLine("{");
        code.Indent();

        // Generate each event property
        foreach (var prop in eventProperties)
        {
            var propType = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propName = prop.Name;

            code.AppendLine($"private static readonly {propType} _{char.ToLower(propName[0])}{propName.Substring(1)} = new();");
            code.AppendLine();
            code.AppendLine($"public static partial {propType} {propName}");
            code.AppendLine("{");
            code.Indent();
            code.AppendLine($"get => _{char.ToLower(propName[0])}{propName.Substring(1)};");
            code.Unindent();
            code.AppendLine("}");
            code.AppendLine();
        }

        code.Unindent();
        code.AppendLine("}");

        return code.ToString();
    }

    private static string GetTypeKind(INamedTypeSymbol symbol)
    {
        return symbol.TypeKind switch
        {
            TypeKind.Class => symbol.IsRecord ? "record class" : "class",
            TypeKind.Struct => symbol.IsRecord ? "record struct" : "struct",
            _ => "class"
        };
    }

    private static string GetAccessibility(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal"
        };
    }

    private class ObserverConfig
    {
        public int Threading { get; set; } = 1; // Locking
        public int Exceptions { get; set; } = 0; // Continue
        public int Order { get; set; } = 0; // RegistrationOrder
        public bool GenerateAsync { get; set; } = true;
        public bool ForceAsync { get; set; } = false;
    }

    private class CodeBuilder
    {
        private readonly System.Text.StringBuilder _sb = new();
        private int _indentLevel = 0;
        private const string IndentString = "    ";

        public void Indent() => _indentLevel++;
        public void Unindent() => _indentLevel = Math.Max(0, _indentLevel - 1);

        public void Append(string text) => _sb.Append(text);

        public void AppendLine(string text = "")
        {
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < _indentLevel; i++)
                    _sb.Append(IndentString);
                _sb.Append(text);
            }
            _sb.AppendLine();
        }

        public override string ToString() => _sb.ToString();
    }
}
