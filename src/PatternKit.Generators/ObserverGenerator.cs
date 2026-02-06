using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace PatternKit.Generators;

/// <summary>
/// Source generator for the Observer pattern.
/// Generates Subscribe, Publish, and PublishAsync methods with snapshot-based iteration
/// and IDisposable subscription tokens.
/// </summary>
[Generator]
public sealed class ObserverGenerator : IIncrementalGenerator
{
    private const string DiagIdTypeNotPartial = "PKOBS001";
    private const string DiagIdAsyncUnsupported = "PKOBS004";
    private const string DiagIdInvalidConfigCombination = "PKOBS005";

    private static readonly DiagnosticDescriptor TypeNotPartialDescriptor = new(
        id: DiagIdTypeNotPartial,
        title: "Type marked with [Observer] must be partial",
        messageFormat: "Type '{0}' is marked with [Observer] but is not declared as partial. Add the 'partial' keyword to the type declaration.",
        category: "PatternKit.Generators.Observer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor AsyncUnsupportedDescriptor = new(
        id: DiagIdAsyncUnsupported,
        title: "Async not supported with SingleThreadedFast threading policy",
        messageFormat: "Type '{0}' has ForceAsync=true but uses SingleThreadedFast threading which does not support async publish. Use Locking or Concurrent threading instead.",
        category: "PatternKit.Generators.Observer",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidConfigCombinationDescriptor = new(
        id: DiagIdInvalidConfigCombination,
        title: "Invalid observer configuration combination",
        messageFormat: "Type '{0}' has an invalid configuration: {1}",
        category: "PatternKit.Generators.Observer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "PatternKit.Generators.Observer.ObserverAttribute",
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, _) => ctx
        );

        context.RegisterSourceOutput(targets, (spc, typeContext) =>
        {
            if (typeContext.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return;

            var attr = typeContext.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == "PatternKit.Generators.Observer.ObserverAttribute");
            if (attr is null)
                return;

            GenerateObserver(spc, typeSymbol, attr, typeContext.TargetNode);
        });
    }

    private void GenerateObserver(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        AttributeData attribute,
        SyntaxNode node)
    {
        if (!GeneratorUtilities.IsPartialType(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeNotPartialDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
            return;
        }

        var config = ParseConfig(attribute);
        if (config.PayloadType is null)
            return;

        // Validate config
        if (config.Threading == 0 && (config.ForceAsync || config.GenerateAsync))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AsyncUnsupportedDescriptor,
                node.GetLocation(),
                typeSymbol.Name));
        }

        var needsAsync = config.ForceAsync || config.GenerateAsync;

        var source = EmitSource(typeSymbol, config, needsAsync);
        var fileName = $"{typeSymbol.Name}.Observer.g.cs";
        context.AddSource(fileName, source);
    }

    private ObserverConfig ParseConfig(AttributeData attribute)
    {
        var config = new ObserverConfig();

        if (attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is INamedTypeSymbol payloadType)
        {
            config.PayloadType = payloadType;
        }

        foreach (var namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "Threading":
                    config.Threading = namedArg.Value.Value is int t ? t : 1;
                    break;
                case "Exceptions":
                    config.Exceptions = namedArg.Value.Value is int e ? e : 1;
                    break;
                case "Order":
                    config.Order = namedArg.Value.Value is int o ? o : 0;
                    break;
                case "GenerateAsync":
                    config.GenerateAsync = namedArg.Value.Value is bool ga && ga;
                    break;
                case "ForceAsync":
                    config.ForceAsync = namedArg.Value.Value is bool fa && fa;
                    break;
            }
        }

        return config;
    }

    private string EmitSource(INamedTypeSymbol typeSymbol, ObserverConfig config, bool needsAsync)
    {
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var typeName = typeSymbol.Name;
        var typeKind = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "class";
        var recordKeyword = typeSymbol.IsRecord ? "record " : "";
        var payloadFqn = config.PayloadType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var useLocking = config.Threading == 1;
        var useConcurrent = config.Threading == 2;
        var exceptionPolicy = config.Exceptions;

        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"partial {recordKeyword}{typeKind} {typeName}");
        sb.AppendLine("{");

        // Fields
        if (useConcurrent)
        {
            sb.AppendLine($"    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Action<{payloadFqn}>> _syncSubscribers = new();");
            if (needsAsync)
                sb.AppendLine($"    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Func<{payloadFqn}, System.Threading.Tasks.ValueTask>> _asyncSubscribers = new();");
            sb.AppendLine("    private int _nextId;");
        }
        else
        {
            sb.AppendLine($"    private readonly System.Collections.Generic.List<(int Id, System.Action<{payloadFqn}> Handler)> _syncSubscribers = new();");
            if (needsAsync)
                sb.AppendLine($"    private readonly System.Collections.Generic.List<(int Id, System.Func<{payloadFqn}, System.Threading.Tasks.ValueTask> Handler)> _asyncSubscribers = new();");
            if (useLocking)
                sb.AppendLine("    private readonly object _subscriberLock = new();");
            sb.AppendLine("    private int _nextId;");
        }

        sb.AppendLine();

        // Subscribe (sync)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Subscribes a synchronous handler. Returns an IDisposable that removes the subscription when disposed.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public System.IDisposable Subscribe(System.Action<{payloadFqn}> handler)");
        sb.AppendLine("    {");
        sb.AppendLine("        var id = System.Threading.Interlocked.Increment(ref _nextId);");
        if (useConcurrent)
        {
            sb.AppendLine("        _syncSubscribers.TryAdd(id, handler);");
        }
        else if (useLocking)
        {
            sb.AppendLine("        lock (_subscriberLock)");
            sb.AppendLine("        {");
            sb.AppendLine("            _syncSubscribers.Add((id, handler));");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine("        _syncSubscribers.Add((id, handler));");
        }
        sb.AppendLine("        return new Subscription(this, id, isAsync: false);");
        sb.AppendLine("    }");

        // Subscribe (async)
        if (needsAsync)
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Subscribes an asynchronous handler. Returns an IDisposable that removes the subscription when disposed.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public System.IDisposable Subscribe(System.Func<{payloadFqn}, System.Threading.Tasks.ValueTask> handler)");
            sb.AppendLine("    {");
            sb.AppendLine("        var id = System.Threading.Interlocked.Increment(ref _nextId);");
            if (useConcurrent)
            {
                sb.AppendLine("        _asyncSubscribers.TryAdd(id, handler);");
            }
            else if (useLocking)
            {
                sb.AppendLine("        lock (_subscriberLock)");
                sb.AppendLine("        {");
                sb.AppendLine("            _asyncSubscribers.Add((id, handler));");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine("        _asyncSubscribers.Add((id, handler));");
            }
            sb.AppendLine("        return new Subscription(this, id, isAsync: true);");
            sb.AppendLine("    }");
        }

        // Publish (sync)
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Publishes the event to all synchronous subscribers using snapshot semantics.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public void Publish({payloadFqn} payload)");
        sb.AppendLine("    {");
        EmitSnapshotSync(sb, payloadFqn, useLocking, useConcurrent);
        EmitPublishLoop(sb, exceptionPolicy, isAsync: false);
        sb.AppendLine("    }");

        // PublishAsync
        if (needsAsync)
        {
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Publishes the event to all subscribers (sync and async) using snapshot semantics.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public async System.Threading.Tasks.ValueTask PublishAsync({payloadFqn} payload, System.Threading.CancellationToken ct = default)");
            sb.AppendLine("    {");
            EmitSnapshotBoth(sb, payloadFqn, useLocking, useConcurrent);
            EmitPublishLoopAsync(sb, exceptionPolicy);
            sb.AppendLine("    }");
        }

        // Unsubscribe method
        sb.AppendLine();
        sb.AppendLine("    private void Unsubscribe(int id, bool isAsync)");
        sb.AppendLine("    {");
        if (useConcurrent)
        {
            sb.AppendLine("        if (isAsync)");
            if (needsAsync)
                sb.AppendLine("            _asyncSubscribers.TryRemove(id, out _);");
            else
                sb.AppendLine("            { }");
            sb.AppendLine("        else");
            sb.AppendLine("            _syncSubscribers.TryRemove(id, out _);");
        }
        else if (useLocking)
        {
            sb.AppendLine("        lock (_subscriberLock)");
            sb.AppendLine("        {");
            if (needsAsync)
            {
                sb.AppendLine("            if (isAsync)");
                sb.AppendLine("            {");
                sb.AppendLine("                for (int i = _asyncSubscribers.Count - 1; i >= 0; i--)");
                sb.AppendLine("                {");
                sb.AppendLine("                    if (_asyncSubscribers[i].Id == id)");
                sb.AppendLine("                    {");
                sb.AppendLine("                        _asyncSubscribers.RemoveAt(i);");
                sb.AppendLine("                        break;");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("            else");
            }
            else
            {
                sb.AppendLine("            // Only sync subscribers");
            }
            sb.AppendLine("            {");
            sb.AppendLine("                for (int i = _syncSubscribers.Count - 1; i >= 0; i--)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (_syncSubscribers[i].Id == id)");
            sb.AppendLine("                    {");
            sb.AppendLine("                        _syncSubscribers.RemoveAt(i);");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }
        else
        {
            if (needsAsync)
            {
                sb.AppendLine("        if (isAsync)");
                sb.AppendLine("        {");
                sb.AppendLine("            for (int i = _asyncSubscribers.Count - 1; i >= 0; i--)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (_asyncSubscribers[i].Id == id)");
                sb.AppendLine("                {");
                sb.AppendLine("                    _asyncSubscribers.RemoveAt(i);");
                sb.AppendLine("                    break;");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("        else");
            }
            sb.AppendLine("        {");
            sb.AppendLine("            for (int i = _syncSubscribers.Count - 1; i >= 0; i--)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (_syncSubscribers[i].Id == id)");
            sb.AppendLine("                {");
            sb.AppendLine("                    _syncSubscribers.RemoveAt(i);");
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }
        sb.AppendLine("    }");

        // Subscription class
        sb.AppendLine();
        sb.AppendLine("    private sealed class Subscription : System.IDisposable");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {typeName} _owner;");
        sb.AppendLine("        private readonly int _id;");
        sb.AppendLine("        private readonly bool _isAsync;");
        sb.AppendLine("        private bool _disposed;");
        sb.AppendLine();
        sb.AppendLine($"        public Subscription({typeName} owner, int id, bool isAsync)");
        sb.AppendLine("        {");
        sb.AppendLine("            _owner = owner;");
        sb.AppendLine("            _id = id;");
        sb.AppendLine("            _isAsync = isAsync;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public void Dispose()");
        sb.AppendLine("        {");
        sb.AppendLine("            if (_disposed) return;");
        sb.AppendLine("            _disposed = true;");
        sb.AppendLine("            _owner.Unsubscribe(_id, _isAsync);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void EmitSnapshotSync(StringBuilder sb, string payloadFqn, bool useLocking, bool useConcurrent)
    {
        if (useConcurrent)
        {
            sb.AppendLine("        var values = _syncSubscribers.Values;");
            sb.AppendLine($"        var snapshot = new System.Action<{payloadFqn}>[values.Count];");
            sb.AppendLine("        values.CopyTo(snapshot, 0);");
        }
        else if (useLocking)
        {
            sb.AppendLine($"        System.Action<{payloadFqn}>[] snapshot;");
            sb.AppendLine("        lock (_subscriberLock)");
            sb.AppendLine("        {");
            sb.AppendLine($"            snapshot = new System.Action<{payloadFqn}>[_syncSubscribers.Count];");
            sb.AppendLine("            for (int i = 0; i < _syncSubscribers.Count; i++)");
            sb.AppendLine("                snapshot[i] = _syncSubscribers[i].Handler;");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        var snapshot = new System.Action<{payloadFqn}>[_syncSubscribers.Count];");
            sb.AppendLine("        for (int i = 0; i < _syncSubscribers.Count; i++)");
            sb.AppendLine("            snapshot[i] = _syncSubscribers[i].Handler;");
        }
    }

    private void EmitSnapshotBoth(StringBuilder sb, string payloadFqn, bool useLocking, bool useConcurrent)
    {
        if (useConcurrent)
        {
            sb.AppendLine("        var syncValues = _syncSubscribers.Values;");
            sb.AppendLine($"        var syncSnapshot = new System.Action<{payloadFqn}>[syncValues.Count];");
            sb.AppendLine("        syncValues.CopyTo(syncSnapshot, 0);");
            sb.AppendLine("        var asyncValues = _asyncSubscribers.Values;");
            sb.AppendLine($"        var asyncSnapshot = new System.Func<{payloadFqn}, System.Threading.Tasks.ValueTask>[asyncValues.Count];");
            sb.AppendLine("        asyncValues.CopyTo(asyncSnapshot, 0);");
        }
        else if (useLocking)
        {
            sb.AppendLine($"        System.Action<{payloadFqn}>[] syncSnapshot;");
            sb.AppendLine($"        System.Func<{payloadFqn}, System.Threading.Tasks.ValueTask>[] asyncSnapshot;");
            sb.AppendLine("        lock (_subscriberLock)");
            sb.AppendLine("        {");
            sb.AppendLine($"            syncSnapshot = new System.Action<{payloadFqn}>[_syncSubscribers.Count];");
            sb.AppendLine("            for (int i = 0; i < _syncSubscribers.Count; i++)");
            sb.AppendLine("                syncSnapshot[i] = _syncSubscribers[i].Handler;");
            sb.AppendLine($"            asyncSnapshot = new System.Func<{payloadFqn}, System.Threading.Tasks.ValueTask>[_asyncSubscribers.Count];");
            sb.AppendLine("            for (int i = 0; i < _asyncSubscribers.Count; i++)");
            sb.AppendLine("                asyncSnapshot[i] = _asyncSubscribers[i].Handler;");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine($"        var syncSnapshot = new System.Action<{payloadFqn}>[_syncSubscribers.Count];");
            sb.AppendLine("        for (int i = 0; i < _syncSubscribers.Count; i++)");
            sb.AppendLine("            syncSnapshot[i] = _syncSubscribers[i].Handler;");
            sb.AppendLine($"        var asyncSnapshot = new System.Func<{payloadFqn}, System.Threading.Tasks.ValueTask>[_asyncSubscribers.Count];");
            sb.AppendLine("        for (int i = 0; i < _asyncSubscribers.Count; i++)");
            sb.AppendLine("            asyncSnapshot[i] = _asyncSubscribers[i].Handler;");
        }
    }

    private void EmitPublishLoop(StringBuilder sb, int exceptionPolicy, bool isAsync)
    {
        if (exceptionPolicy == 0) // Stop
        {
            sb.AppendLine("        for (int i = 0; i < snapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            snapshot[i](payload);");
            sb.AppendLine("        }");
        }
        else if (exceptionPolicy == 1) // Continue
        {
            sb.AppendLine("        System.Exception? firstException = null;");
            sb.AppendLine("        for (int i = 0; i < snapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { snapshot[i](payload); }");
            sb.AppendLine("            catch (System.Exception ex) { firstException ??= ex; }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (firstException != null)");
            sb.AppendLine("            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstException).Throw();");
        }
        else // Aggregate
        {
            sb.AppendLine("        var exceptions = new System.Collections.Generic.List<System.Exception>();");
            sb.AppendLine("        for (int i = 0; i < snapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { snapshot[i](payload); }");
            sb.AppendLine("            catch (System.Exception ex) { exceptions.Add(ex); }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (exceptions.Count > 0)");
            sb.AppendLine("            throw new System.AggregateException(exceptions);");
        }
    }

    private void EmitPublishLoopAsync(StringBuilder sb, int exceptionPolicy)
    {
        if (exceptionPolicy == 0) // Stop
        {
            sb.AppendLine("        for (int i = 0; i < syncSnapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            ct.ThrowIfCancellationRequested();");
            sb.AppendLine("            syncSnapshot[i](payload);");
            sb.AppendLine("        }");
            sb.AppendLine("        for (int i = 0; i < asyncSnapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            ct.ThrowIfCancellationRequested();");
            sb.AppendLine("            await asyncSnapshot[i](payload).ConfigureAwait(false);");
            sb.AppendLine("        }");
        }
        else if (exceptionPolicy == 1) // Continue
        {
            sb.AppendLine("        System.Exception? firstException = null;");
            sb.AppendLine("        for (int i = 0; i < syncSnapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { syncSnapshot[i](payload); }");
            sb.AppendLine("            catch (System.Exception ex) { firstException ??= ex; }");
            sb.AppendLine("        }");
            sb.AppendLine("        for (int i = 0; i < asyncSnapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { await asyncSnapshot[i](payload).ConfigureAwait(false); }");
            sb.AppendLine("            catch (System.Exception ex) { firstException ??= ex; }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (firstException != null)");
            sb.AppendLine("            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstException).Throw();");
        }
        else // Aggregate
        {
            sb.AppendLine("        var exceptions = new System.Collections.Generic.List<System.Exception>();");
            sb.AppendLine("        for (int i = 0; i < syncSnapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { syncSnapshot[i](payload); }");
            sb.AppendLine("            catch (System.Exception ex) { exceptions.Add(ex); }");
            sb.AppendLine("        }");
            sb.AppendLine("        for (int i = 0; i < asyncSnapshot.Length; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            try { await asyncSnapshot[i](payload).ConfigureAwait(false); }");
            sb.AppendLine("            catch (System.Exception ex) { exceptions.Add(ex); }");
            sb.AppendLine("        }");
            sb.AppendLine("        if (exceptions.Count > 0)");
            sb.AppendLine("            throw new System.AggregateException(exceptions);");
        }
    }

    private class ObserverConfig
    {
        public INamedTypeSymbol? PayloadType { get; set; }
        public int Threading { get; set; } = 1;
        public int Exceptions { get; set; } = 1;
        public int Order { get; set; } = 0;
        public bool GenerateAsync { get; set; }
        public bool ForceAsync { get; set; }
    }
}
