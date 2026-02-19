using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators.Observer;

/// <summary>
/// Incremental source generator for the Observer pattern.
/// Generates Subscribe/Publish methods with configurable threading, exception, and ordering policies.
/// </summary>
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

        var attr = occurrence.Attributes.Length > 0 ? occurrence.Attributes[0] : null;
        if (attr == null || attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not INamedTypeSymbol payloadType)
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
                    config.Threading = (int)arg.Value.Value!;
                    break;
                case "Exceptions":
                    config.Exceptions = (int)arg.Value.Value!;
                    break;
                case "Order":
                    config.Order = (int)arg.Value.Value!;
                    break;
                case "GenerateAsync":
                    config.GenerateAsync = (bool)arg.Value.Value!;
                    break;
                case "ForceAsync":
                    config.ForceAsync = (bool)arg.Value.Value!;
                    break;
            }
        }
        return config;
    }

    private static string GenerateSource(INamedTypeSymbol typeSymbol, INamedTypeSymbol payloadType, ObserverConfig config)
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
        
        // Add necessary using directives
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

        var isStruct = typeSymbol.TypeKind == TypeKind.Struct;

        sb.AppendLine($"{accessibility} partial {typeKind} {typeName}");
        sb.AppendLine("{");

        GenerateFields(sb, config, isStruct);
        GenerateSubscribeMethods(sb, payloadTypeName, config);
        GeneratePublishMethods(sb, payloadTypeName, config);
        GenerateUnsubscribeMethod(sb, config);
        GenerateOnErrorHook(sb);
        GenerateSubscriptionClass(sb, payloadTypeName, config);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateFields(StringBuilder sb, ObserverConfig config, bool isStruct)
    {
        // For all types, use nullable fields and ensure initialization in helper methods
        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                sb.AppendLine("    private System.Collections.Generic.List<Subscription>? _subscriptions;");
                sb.AppendLine("    private int _nextId;");
                break;

            case 1: // Locking
                sb.AppendLine("    private object? _lock;");
                sb.AppendLine("    private System.Collections.Generic.List<Subscription>? _subscriptions;");
                sb.AppendLine("    private int _nextId;");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    sb.AppendLine("    private System.Collections.Immutable.ImmutableList<Subscription>? _subscriptions;");
                    sb.AppendLine("    private int _nextId;");
                }
                else // Undefined
                {
                    sb.AppendLine("    private System.Collections.Concurrent.ConcurrentBag<Subscription>? _subscriptions;");
                    sb.AppendLine("    private int _nextId;");
                }
                break;
        }
        sb.AppendLine();
    }

    private static void GenerateSubscribeMethods(StringBuilder sb, string payloadType, ObserverConfig config)
    {
        if (!config.ForceAsync)
        {
            sb.AppendLine($"    public System.IDisposable Subscribe(System.Action<{payloadType}> handler)");
            sb.AppendLine("    {");
            sb.AppendLine("        var id = System.Threading.Interlocked.Increment(ref _nextId);");
            sb.AppendLine("        var sub = new Subscription(this, id, handler, false);");
            GenerateAddSubscription(sb, config, "        ");
            sb.AppendLine("        return sub;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        if (config.GenerateAsync)
        {
            sb.AppendLine($"    public System.IDisposable Subscribe(System.Func<{payloadType}, System.Threading.Tasks.ValueTask> handler)");
            sb.AppendLine("    {");
            sb.AppendLine("        var id = System.Threading.Interlocked.Increment(ref _nextId);");
            sb.AppendLine("        var sub = new Subscription(this, id, handler, true);");
            GenerateAddSubscription(sb, config, "        ");
            sb.AppendLine("        return sub;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
    }

    private static void GenerateAddSubscription(StringBuilder sb, ObserverConfig config, string indent)
    {
        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                sb.AppendLine($"{indent}(_subscriptions ??= new()).Add(sub);");
                break;

            case 1: // Locking
                sb.AppendLine($"{indent}lock (_lock ??= new())");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    (_subscriptions ??= new()).Add(sub);");
                sb.AppendLine($"{indent}}}");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    sb.AppendLine($"{indent}System.Collections.Immutable.ImmutableInterlocked.Update(ref _subscriptions, static (list, s) => (list ?? System.Collections.Immutable.ImmutableList<Subscription>.Empty).Add(s), sub);");
                }
                else // Undefined
                {
                    sb.AppendLine($"{indent}(_subscriptions ??= new()).Add(sub);");
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
            sb.AppendLine("            if (sub.IsAsync) continue;");

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
                sb.AppendLine($"{indent}var snapshot = _subscriptions?.ToArray() ?? System.Array.Empty<Subscription>();");
                break;

            case 1: // Locking
                sb.AppendLine($"{indent}Subscription[] snapshot;");
                sb.AppendLine($"{indent}var lockObj = _lock ??= new object();");
                sb.AppendLine($"{indent}lock (lockObj)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    snapshot = _subscriptions?.ToArray() ?? System.Array.Empty<Subscription>();");
                sb.AppendLine($"{indent}}}");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    sb.AppendLine($"{indent}var snapshot = System.Threading.Volatile.Read(ref _subscriptions)?.ToArray() ?? System.Array.Empty<Subscription>();");
                }
                else // Undefined
                {
                    sb.AppendLine($"{indent}var snapshot = _subscriptions?.ToArray() ?? System.Array.Empty<Subscription>();");
                }
                break;
        }
    }

    private static void GenerateUnsubscribeMethod(StringBuilder sb, ObserverConfig config)
    {
        sb.AppendLine("    private void Unsubscribe(int id)");
        sb.AppendLine("    {");

        switch (config.Threading)
        {
            case 0: // SingleThreadedFast
                sb.AppendLine("        _subscriptions?.RemoveAll(s => s.Id == id);");
                break;

            case 1: // Locking
                sb.AppendLine("        var lockObj = _lock ??= new object();");
                sb.AppendLine("        lock (lockObj)");
                sb.AppendLine("        {");
                sb.AppendLine("            _subscriptions?.RemoveAll(s => s.Id == id);");
                sb.AppendLine("        }");
                break;

            case 2: // Concurrent
                if (config.Order == 0) // RegistrationOrder
                {
                    sb.AppendLine("        System.Collections.Immutable.ImmutableInterlocked.Update(ref _subscriptions, static (list, id) => list?.RemoveAll(s => s.Id == id) ?? list, id);");
                }
                else // Undefined - ConcurrentBag doesn't support efficient removal
                {
                    sb.AppendLine("        // ConcurrentBag doesn't support removal; subscription marks itself disposed");
                }
                break;
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateOnErrorHook(StringBuilder sb)
    {
        sb.AppendLine("    partial void OnSubscriberError(System.Exception ex);");
        sb.AppendLine();
    }

    private static void GenerateSubscriptionClass(StringBuilder sb, string payloadType, ObserverConfig config)
    {
        // Use generic parent reference to avoid dynamic
        sb.AppendLine($"    private sealed class Subscription : System.IDisposable");
        sb.AppendLine("    {");
        sb.AppendLine("        private object? _parent;");
        sb.AppendLine("        private readonly int _id;");
        sb.AppendLine("        private readonly object _handler;");
        sb.AppendLine("        private readonly bool _isAsync;");
        sb.AppendLine("        private int _disposed;");
        sb.AppendLine();
        sb.AppendLine("        public int Id => _id;");
        sb.AppendLine("        public bool IsAsync => _isAsync;");
        sb.AppendLine();
        sb.AppendLine("        public Subscription(object parent, int id, object handler, bool isAsync)");
        sb.AppendLine("        {");
        sb.AppendLine("            _parent = parent;");
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
        sb.AppendLine("            // Use reflection to call Unsubscribe since parent type is generic");
        sb.AppendLine("            var parent = System.Threading.Interlocked.Exchange(ref _parent, null);");
        sb.AppendLine("            if (parent == null) return;");
        sb.AppendLine("            var method = parent.GetType().GetMethod(\"Unsubscribe\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);");
        sb.AppendLine("            method?.Invoke(parent, new object[] { _id });");
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
