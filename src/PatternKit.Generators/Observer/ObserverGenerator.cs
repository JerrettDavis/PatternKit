using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators.Observer;

/// <summary>
/// Incremental source generator for the Observer pattern.
/// Generates Subscribe/Publish methods with configurable threading, exception, and ordering policies.
/// </summary>
/// <remarks>
/// This generator currently implements only single-event observer types via [Observer].
/// Hub-based generation via [ObserverHub] and [ObservedEvent] is reserved for future implementation.
/// </remarks>
[Generator]
public sealed class ObserverGenerator : IIncrementalGenerator
{
    private const string DiagnosticIdNotPartial = "PKOBS001";
    private const string DiagnosticIdMissingPayload = "PKOBS002";
    private const string DiagnosticIdInvalidConfig = "PKOBS003";

    private static readonly DiagnosticDescriptor NotPartialRule = new(
        DiagnosticIdNotPartial,
        "Type must be partial",
        "Type '{0}' marked with [Observer] must be declared as partial",
        "PatternKit.Generators.Observer",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingPayloadRule = new(
        DiagnosticIdMissingPayload,
        "Missing payload type",
        "Unable to extract payload type from [Observer] attribute on '{0}'",
        "PatternKit.Generators.Observer",
        DiagnosticSeverity.Error,
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
        var observerTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Observer.ObserverAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(observerTypes, static (spc, occ) =>
        {
            GenerateObserver(spc, occ);
        });
    }

    private static void GenerateObserver(SourceProductionContext context, GeneratorAttributeSyntaxContext occurrence)
    {
        var typeSymbol = (INamedTypeSymbol)occurrence.TargetSymbol;
        var syntax = (TypeDeclarationSyntax)occurrence.TargetNode;

        if (!IsPartial(syntax))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NotPartialRule,
                syntax.Identifier.GetLocation(),
                typeSymbol.Name));
            return;
        }

        // Check for generic types
        if (typeSymbol.IsGenericType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidConfigRule,
                syntax.Identifier.GetLocation(),
                "Generic observer types are not supported"));
            return;
        }

        // Check for nested types
        if (typeSymbol.ContainingType != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidConfigRule,
                syntax.Identifier.GetLocation(),
                "Nested observer types are not supported"));
            return;
        }

        // Structs have complex lifetime and capture semantics, especially with fire-and-forget async
        if (typeSymbol.TypeKind == TypeKind.Struct)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidConfigRule,
                syntax.Identifier.GetLocation(),
                "Struct observer types are not currently supported due to capture and boxing complexity"));
            return;
        }

        var attr = occurrence.Attributes.Length > 0 ? occurrence.Attributes[0] : null;
        if (attr == null || attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not ITypeSymbol payloadType)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MissingPayloadRule,
                syntax.Identifier.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var config = ExtractConfig(attr);
        var source = GenerateSource(typeSymbol, payloadType, config);
        var fileName = $"{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "").Replace("<", "_").Replace(">", "_").Replace(".", "_")}.Observer.g.cs";
        context.AddSource(fileName, source);
    }

    private static bool IsPartial(TypeDeclarationSyntax syntax)
        => syntax.Modifiers.Any(m => m.Text == "partial");

    private static ObserverConfig ExtractConfig(AttributeData attr)
    {
        var config = new ObserverConfig();
        foreach (var arg in attr.NamedArguments)
        {
            switch (arg.Key)
            {
                case "Threading":
                    var threading = (int)arg.Value.Value!;
                    config.Threading = threading >= 0 && threading <= 2 ? threading : 1; // Default to Locking if invalid
                    break;
                case "Exceptions":
                    var exceptions = (int)arg.Value.Value!;
                    config.Exceptions = exceptions >= 0 && exceptions <= 2 ? exceptions : 0; // Default to Continue if invalid
                    break;
                case "Order":
                    var order = (int)arg.Value.Value!;
                    config.Order = order >= 0 && order <= 1 ? order : 0; // Default to RegistrationOrder if invalid
                    break;
                case "GenerateAsync":
                    config.GenerateAsync = (bool)arg.Value.Value!;
                    break;
                case "ForceAsync":
                    config.ForceAsync = (bool)arg.Value.Value!;
                    break;
            }
        }
        
        // If ForceAsync is true but GenerateAsync is false, enable GenerateAsync
        if (config.ForceAsync && !config.GenerateAsync)
        {
            config.GenerateAsync = true;
        }
        
        return config;
    }

    private static string GenerateSource(INamedTypeSymbol typeSymbol, ITypeSymbol payloadType, ObserverConfig config)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeKind = typeSymbol.TypeKind switch
        {
            TypeKind.Class => typeSymbol.IsRecord ? "record class" : "class",
            TypeKind.Struct => typeSymbol.IsRecord ? "record struct" : "struct",
            _ => "class"
        };

        var accessibility = typeSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            _ => "internal"
        };

        var typeName = typeSymbol.Name;
        var payloadTypeName = payloadType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();
        
        // Add using System.Linq for ToArray() extension method on ImmutableList
        if (config.Threading == 2 && config.Order == 0) // Concurrent + RegistrationOrder uses ImmutableList
        {
            sb.AppendLine("using System.Linq;");
            sb.AppendLine();
        }

        if (ns != null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"{accessibility} partial {typeKind} {typeName}");
        sb.AppendLine("{");

        GenerateFields(sb, config);
        GenerateSubscribeMethods(sb, payloadTypeName, config);
        GeneratePublishMethods(sb, payloadTypeName, config);
        GenerateOnErrorHook(sb);
        GenerateSubscriptionClass(sb, payloadTypeName, config);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateFields(StringBuilder sb, ObserverConfig config)
    {
        // Generate a shared state object to avoid any issues with subscriptions
        sb.AppendLine("    private sealed class ObserverState");
        sb.AppendLine("    {");
        
        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                sb.AppendLine("        public System.Collections.Generic.List<Subscription>? Subscriptions;");
                sb.AppendLine("        public int NextId;");
                break;

            case 1: // Locking
                sb.AppendLine("        public readonly object Lock = new();");
                sb.AppendLine("        public System.Collections.Generic.List<Subscription>? Subscriptions;");
                sb.AppendLine("        public int NextId;");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    sb.AppendLine("        public System.Collections.Immutable.ImmutableList<Subscription>? Subscriptions;");
                    sb.AppendLine("        public int NextId;");
                }
                else // Undefined
                {
                    sb.AppendLine("        public System.Collections.Concurrent.ConcurrentBag<Subscription>? Subscriptions;");
                    sb.AppendLine("        public int NextId;");
                }
                break;
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private readonly ObserverState _state = new();");
        sb.AppendLine();
    }

    private static void GenerateSubscribeMethods(StringBuilder sb, string payloadType, ObserverConfig config)
    {        
        if (!config.ForceAsync)
        {
            sb.AppendLine($"    public System.IDisposable Subscribe(System.Action<{payloadType}> handler)");
            sb.AppendLine("    {");
            sb.AppendLine("        var id = System.Threading.Interlocked.Increment(ref _state.NextId);");
            sb.AppendLine("        var sub = new Subscription(_state, id, handler, false);");
            GenerateAddSubscription(sb, config, "        ", "_state");
            sb.AppendLine("        return sub;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        if (config.GenerateAsync)
        {
            sb.AppendLine($"    public System.IDisposable Subscribe(System.Func<{payloadType}, System.Threading.Tasks.ValueTask> handler)");
            sb.AppendLine("    {");
            sb.AppendLine("        var id = System.Threading.Interlocked.Increment(ref _state.NextId);");
            sb.AppendLine("        var sub = new Subscription(_state, id, handler, true);");
            GenerateAddSubscription(sb, config, "        ", "_state");
            sb.AppendLine("        return sub;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static void GenerateAddSubscription(StringBuilder sb, ObserverConfig config, string indent, string stateVar)
    {
        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                sb.AppendLine($"{indent}({stateVar}.Subscriptions ??= new()).Add(sub);");
                break;

            case 1: // Locking
                sb.AppendLine($"{indent}lock ({stateVar}.Lock)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    ({stateVar}.Subscriptions ??= new()).Add(sub);");
                sb.AppendLine($"{indent}}}");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder (requires ImmutableList and Linq for .ToArray())
                {
                    sb.AppendLine($"{indent}System.Collections.Immutable.ImmutableInterlocked.Update(ref {stateVar}.Subscriptions, static (list, s) => (list ?? System.Collections.Immutable.ImmutableList<Subscription>.Empty).Add(s), sub);");
                }
                else // Undefined
                {
                    sb.AppendLine($"{indent}System.Threading.LazyInitializer.EnsureInitialized(ref {stateVar}.Subscriptions, static () => new System.Collections.Concurrent.ConcurrentBag<Subscription>()).Add(sub);");
                }
                break;
        }
    }

    private static void GeneratePublishMethods(StringBuilder sb, string payloadType, ObserverConfig config)
    {
        if (!config.ForceAsync)
        {
            sb.AppendLine($"    public void Publish({payloadType} payload)");
            sb.AppendLine("    {");
            GenerateSnapshot(sb, config, "        ");
            sb.AppendLine();

            if (config.Exceptions == 2) // Aggregate
            {
                sb.AppendLine("        System.Collections.Generic.List<System.Exception>? errors = null;");
                sb.AppendLine();
            }

            sb.AppendLine("        foreach (var sub in snapshot)");
            sb.AppendLine("        {");
            
            // Handle async subscriptions in fire-and-forget mode
            sb.AppendLine("            if (sub.IsAsync)");
            sb.AppendLine("            {");
            if (config.Exceptions == 0) // Continue - fire and forget with error handling
            {
                sb.AppendLine("                _ = System.Threading.Tasks.Task.Run(async () =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        await sub.InvokeAsync(payload, System.Threading.CancellationToken.None).ConfigureAwait(false);");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch (System.Exception ex)");
                sb.AppendLine("                    {");
                sb.AppendLine("                        OnSubscriberError(ex);");
                sb.AppendLine("                    }");
                sb.AppendLine("                });");
            }
            else if (config.Exceptions == 1) // Stop - fire and forget; exceptions are unobserved
            {
                sb.AppendLine("                // Fire-and-forget: exceptions from async handlers cannot stop sync execution");
                sb.AppendLine("                _ = System.Threading.Tasks.Task.Run(async () =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    await sub.InvokeAsync(payload, System.Threading.CancellationToken.None).ConfigureAwait(false);");
                sb.AppendLine("                });");
            }
            else // Aggregate - fire and forget with error logging via OnSubscriberError
            {
                sb.AppendLine("                // Fire-and-forget: async exceptions logged via OnSubscriberError (cannot aggregate synchronously)");
                sb.AppendLine("                _ = System.Threading.Tasks.Task.Run(async () =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    try");
                sb.AppendLine("                    {");
                sb.AppendLine("                        await sub.InvokeAsync(payload, System.Threading.CancellationToken.None).ConfigureAwait(false);");
                sb.AppendLine("                    }");
                sb.AppendLine("                    catch (System.Exception ex)");
                sb.AppendLine("                    {");
                sb.AppendLine("                        OnSubscriberError(ex);");
                sb.AppendLine("                    }");
                sb.AppendLine("                });");
            }
            sb.AppendLine("                continue;");
            sb.AppendLine("            }");

            if (config.Exceptions == 1) // Stop
            {
                sb.AppendLine("            sub.InvokeSync(payload);");
            }
            else
            {
                sb.AppendLine("            try");
                sb.AppendLine("            {");
                sb.AppendLine("                sub.InvokeSync(payload);");
                sb.AppendLine("            }");
                sb.AppendLine("            catch (System.Exception ex)");
                sb.AppendLine("            {");
                if (config.Exceptions == 0) // Continue
                {
                    sb.AppendLine("                OnSubscriberError(ex);");
                }
                else // Aggregate
                {
                    sb.AppendLine("                (errors ??= new()).Add(ex);");
                }
                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");

            if (config.Exceptions == 2)
            {
                sb.AppendLine();
                sb.AppendLine("        if (errors is { Count: > 0 })");
                sb.AppendLine("            throw new System.AggregateException(errors);");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        if (config.GenerateAsync)
        {
            sb.AppendLine($"    public async System.Threading.Tasks.ValueTask PublishAsync({payloadType} payload, System.Threading.CancellationToken cancellationToken = default)");
            sb.AppendLine("    {");
            GenerateSnapshot(sb, config, "        ");
            sb.AppendLine();

            if (config.Exceptions == 2)
            {
                sb.AppendLine("        System.Collections.Generic.List<System.Exception>? errors = null;");
                sb.AppendLine();
            }

            sb.AppendLine("        foreach (var sub in snapshot)");
            sb.AppendLine("        {");
            sb.AppendLine("            cancellationToken.ThrowIfCancellationRequested();");

            if (config.Exceptions == 1) // Stop
            {
                sb.AppendLine("            await sub.InvokeAsync(payload, cancellationToken).ConfigureAwait(false);");
            }
            else
            {
                sb.AppendLine("            try");
                sb.AppendLine("            {");
                sb.AppendLine("                await sub.InvokeAsync(payload, cancellationToken).ConfigureAwait(false);");
                sb.AppendLine("            }");
                sb.AppendLine("            catch (System.Exception ex)");
                sb.AppendLine("            {");
                if (config.Exceptions == 0) // Continue
                {
                    sb.AppendLine("                OnSubscriberError(ex);");
                }
                else // Aggregate
                {
                    sb.AppendLine("                (errors ??= new()).Add(ex);");
                }
                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");

            if (config.Exceptions == 2)
            {
                sb.AppendLine();
                sb.AppendLine("        if (errors is { Count: > 0 })");
                sb.AppendLine("            throw new System.AggregateException(errors);");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static void GenerateSnapshot(StringBuilder sb, ObserverConfig config, string indent)
    {        
        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                sb.AppendLine($"{indent}var snapshot = _state.Subscriptions?.ToArray() ?? System.Array.Empty<Subscription>();");
                break;

            case 1: // Locking
                sb.AppendLine($"{indent}Subscription[] snapshot;");
                sb.AppendLine($"{indent}lock (_state.Lock)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    snapshot = _state.Subscriptions?.ToArray() ?? System.Array.Empty<Subscription>();");
                sb.AppendLine($"{indent}}}");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    sb.AppendLine($"{indent}var snapshot = System.Threading.Volatile.Read(ref _state.Subscriptions)?.ToArray() ?? System.Array.Empty<Subscription>();");
                }
                else // Undefined
                {
                    sb.AppendLine($"{indent}var snapshot = _state.Subscriptions?.ToArray() ?? System.Array.Empty<Subscription>();");
                }
                break;
        }
    }

    private static void GenerateOnErrorHook(StringBuilder sb)
    {
        sb.AppendLine("    partial void OnSubscriberError(System.Exception ex);");
        sb.AppendLine();
    }

    private static void GenerateSubscriptionClass(StringBuilder sb, string payloadType, ObserverConfig config)
    {
        // Subscription now uses a delegate callback instead of reflection
        sb.AppendLine($"    private sealed class Subscription : System.IDisposable");
        sb.AppendLine("    {");
        sb.AppendLine("        private ObserverState? _state;");
        sb.AppendLine("        private readonly int _id;");
        sb.AppendLine("        private readonly object _handler;");
        sb.AppendLine("        private readonly bool _isAsync;");
        sb.AppendLine("        private int _disposed;");
        sb.AppendLine();
        sb.AppendLine("        public int Id => _id;");
        sb.AppendLine("        public bool IsAsync => _isAsync;");
        sb.AppendLine();
        sb.AppendLine("        public Subscription(ObserverState state, int id, object handler, bool isAsync)");
        sb.AppendLine("        {");
        sb.AppendLine("            _state = state;");
        sb.AppendLine("            _id = id;");
        sb.AppendLine("            _handler = handler;");
        sb.AppendLine("            _isAsync = isAsync;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        public void InvokeSync({payloadType} payload)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (System.Threading.Volatile.Read(ref _disposed) != 0) return;");
        sb.AppendLine($"            ((System.Action<{payloadType}>)_handler)(payload);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        public System.Threading.Tasks.ValueTask InvokeAsync({payloadType} payload, System.Threading.CancellationToken ct)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (System.Threading.Volatile.Read(ref _disposed) != 0) return default;");
        sb.AppendLine("            if (_isAsync)");
        sb.AppendLine($"                return ((System.Func<{payloadType}, System.Threading.Tasks.ValueTask>)_handler)(payload);");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                ((System.Action<{payloadType}>)_handler)(payload);");
        sb.AppendLine("                return default;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public void Dispose()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;");
        sb.AppendLine("            var state = System.Threading.Interlocked.Exchange(ref _state, null);");
        sb.AppendLine("            if (state == null) return;");
        sb.AppendLine();

        // Generate the appropriate unsubscribe logic based on threading policy
        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                sb.AppendLine("            state.Subscriptions?.RemoveAll(s => s.Id == _id);");
                break;

            case 1: // Locking
                sb.AppendLine("            lock (state.Lock)");
                sb.AppendLine("            {");
                sb.AppendLine("                state.Subscriptions?.RemoveAll(s => s.Id == _id);");
                sb.AppendLine("            }");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    sb.AppendLine("            System.Collections.Immutable.ImmutableInterlocked.Update(ref state.Subscriptions, static (list, id) => list?.RemoveAll(s => s.Id == id) ?? list, _id);");
                }
                else
                {
                    sb.AppendLine("            // ConcurrentBag doesn't support efficient removal.");
                    sb.AppendLine("            // Disposed subscriptions remain in the bag but are marked as disposed and won't be invoked.");
                    sb.AppendLine("            // Note: This can cause memory growth if many subscriptions are created and disposed.");
                }
                break;
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private sealed class ObserverConfig
    {
        public int Threading { get; set; } = 1; // Locking
        public int Exceptions { get; set; } = 0; // Continue
        public int Order { get; set; } = 0; // RegistrationOrder
        public bool GenerateAsync { get; set; } = true;
        public bool ForceAsync { get; set; } = false;
    }
}
