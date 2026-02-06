using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PatternKit.Generators.Messaging;

[Generator]
public sealed class DispatcherGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find assembly attributes
        var assemblyAttributes = context.CompilationProvider.Select((compilation, _) =>
        {
            var attr = compilation.Assembly.GetAttributes()
                .Where(a => a.AttributeClass?.Name == "GenerateDispatcherAttribute" &&
                           a.AttributeClass.ContainingNamespace.ToDisplayString() == "PatternKit.Generators.Messaging")
                .FirstOrDefault();

            return (compilation, (AttributeData?)attr);
        });

        context.RegisterSourceOutput(assemblyAttributes, (spc, data) =>
        {
            var (compilation, attr) = data;
            if (attr == null) return;

            if (!TryReadAttribute(attr, out var config, out var error))
            {
                ReportDiagnostic(spc, "PKD006", error ?? "Invalid GenerateDispatcher configuration",
                    DiagnosticSeverity.Error, Location.None);
                return;
            }

            GenerateDispatcher(spc, compilation, config);
        });
    }

    private static bool TryReadAttribute(AttributeData attr, out DispatcherConfig config, out string? error)
    {
        error = null;
        var args = attr.NamedArguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        config = new DispatcherConfig
        {
            Namespace = GetStringValue(args, "Namespace") ?? "Generated.Messaging",
            Name = GetStringValue(args, "Name") ?? "AppDispatcher",
            IncludeObjectOverloads = GetBoolValue(args, "IncludeObjectOverloads", false),
            IncludeStreaming = GetBoolValue(args, "IncludeStreaming", true),
            Visibility = GetIntValue(args, "Visibility", 0)
        };

        return true;
    }

    private static string? GetStringValue(Dictionary<string, TypedConstant> args, string key) =>
        args.TryGetValue(key, out var value) ? value.Value as string : null;

    private static bool GetBoolValue(Dictionary<string, TypedConstant> args, string key, bool defaultValue) =>
        args.TryGetValue(key, out var value) && value.Value is bool b ? b : defaultValue;

    private static int GetIntValue(Dictionary<string, TypedConstant> args, string key, int defaultValue) =>
        args.TryGetValue(key, out var value) && value.Value is int i ? i : defaultValue;

    private static void GenerateDispatcher(SourceProductionContext spc, Compilation compilation, DispatcherConfig config)
    {
        var visibility = config.Visibility == 0 ? "public" : "internal";

        var sources = new[]
        {
            ($"{config.Name}.g.cs", GenerateMainDispatcherFile(config, visibility)),
            ($"{config.Name}.Builder.g.cs", GenerateBuilderFile(config, visibility)),
            ($"{config.Name}.Contracts.g.cs", GenerateContractsFile(config, visibility))
        };

        foreach (var (fileName, source) in sources)
        {
            spc.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateMainDispatcherFile(DispatcherConfig config, string visibility)
    {
        var sb = CreateFileHeader();

        var usings = new List<string>
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Threading",
            "System.Threading.Tasks"
        };

        if (config.IncludeStreaming)
        {
            usings.Add("System.Runtime.CompilerServices");
        }

        if (config.IncludeObjectOverloads)
        {
            usings.Add("System.Reflection");
        }

        AppendUsings(sb, usings.ToArray());
        AppendNamespaceAndClassHeader(sb, config.Namespace, visibility, config.Name);

        // PipelineEntry class
        GeneratePipelineEntry(sb);

        // Internal state
        sb.AppendLine("    private readonly Dictionary<Type, Delegate> _commandHandlers = new();");
        sb.AppendLine("    private readonly Dictionary<Type, List<Delegate>> _notificationHandlers = new();");

        if (config.IncludeStreaming)
        {
            sb.AppendLine("    private readonly Dictionary<Type, Delegate> _streamHandlers = new();");
        }

        sb.AppendLine("    private readonly Dictionary<Type, List<PipelineEntry>> _commandPipelines = new();");

        if (config.IncludeStreaming)
        {
            sb.AppendLine("    private readonly Dictionary<Type, List<PipelineEntry>> _streamPipelines = new();");
        }

        sb.AppendLine();
        sb.AppendLine("    private " + config.Name + "() { }");
        sb.AppendLine();

        // Create method
        sb.AppendLine($"    {visibility} static Builder Create() => new Builder();");
        sb.AppendLine();

        // Send method (commands)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Sends a command and returns a response.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async ValueTask<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        var requestType = typeof(TRequest);");
        sb.AppendLine("        if (!_commandHandlers.TryGetValue(requestType, out var handlerDelegate))");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException($\"No handler registered for command type {requestType.Name}\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var handler = (Func<TRequest, CancellationToken, ValueTask<TResponse>>)handlerDelegate;");
        sb.AppendLine();
        sb.AppendLine("        // Execute pipelines if registered");
        sb.AppendLine("        if (_commandPipelines.TryGetValue(requestType, out var pipelines))");
        sb.AppendLine("        {");
        sb.AppendLine("            return await ExecuteWithPipeline(request, handler, pipelines, ct);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return await handler(request, ct);");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Object overload for Send
        if (config.IncludeObjectOverloads)
        {
            GenerateObjectSendMethod(sb);
        }

        // Publish method (notifications)
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Publishes a notification to all registered handlers.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        var notificationType = typeof(TNotification);");
        sb.AppendLine("        if (!_notificationHandlers.TryGetValue(notificationType, out var handlers))");
        sb.AppendLine("        {");
        sb.AppendLine("            return; // No-op if no handlers");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var handlerDelegate in handlers)");
        sb.AppendLine("        {");
        sb.AppendLine("            var handler = (Func<TNotification, CancellationToken, ValueTask>)handlerDelegate;");
        sb.AppendLine("            await handler(notification, ct);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Object overload for Publish
        if (config.IncludeObjectOverloads)
        {
            GenerateObjectPublishMethod(sb);
        }

        // Stream method
        if (config.IncludeStreaming)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Streams items from a stream request.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public async IAsyncEnumerable<TItem> Stream<TRequest, TItem>(TRequest request, [EnumeratorCancellation] CancellationToken ct = default)");
            sb.AppendLine("    {");
            sb.AppendLine("        var requestType = typeof(TRequest);");
            sb.AppendLine();
            sb.AppendLine("        // Execute Pre hooks if registered");
            sb.AppendLine("        if (_streamPipelines.TryGetValue(requestType, out var pipelines))");
            sb.AppendLine("        {");
            sb.AppendLine("            var orderedPipelines = pipelines.OrderBy(p => p.Order).ToList();");
            sb.AppendLine("            foreach (var entry in orderedPipelines.Where(e => e.Type == PipelineType.Pre))");
            sb.AppendLine("            {");
            sb.AppendLine("                var pre = (Func<TRequest, CancellationToken, ValueTask>)entry.Delegate;");
            sb.AppendLine("                await pre(request, ct);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        if (!_streamHandlers.TryGetValue(requestType, out var handlerDelegate))");
            sb.AppendLine("        {");
            sb.AppendLine("            throw new InvalidOperationException($\"No stream handler registered for request type {requestType.Name}\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        var handler = (Func<TRequest, CancellationToken, IAsyncEnumerable<TItem>>)handlerDelegate;");
            sb.AppendLine("        var stream = handler(request, ct);");
            sb.AppendLine();
            sb.AppendLine("        await foreach (var item in stream.WithCancellation(ct))");
            sb.AppendLine("        {");
            sb.AppendLine("            yield return item;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Object overload for Stream
            if (config.IncludeObjectOverloads)
            {
                GenerateObjectStreamMethod(sb);
            }
        }

        // Helper method for pipeline execution
        GenerateExecuteWithPipelineMethod(sb);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GeneratePipelineEntry(StringBuilder sb)
    {
        sb.AppendLine("    private enum PipelineType { Pre, Around, Post, OnError }");
        sb.AppendLine();
        sb.AppendLine("    private sealed class PipelineEntry");
        sb.AppendLine("    {");
        sb.AppendLine("        public PipelineType Type { get; set; }");
        sb.AppendLine("        public int Order { get; set; }");
        sb.AppendLine("        public Delegate Delegate { get; set; } = null!;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateExecuteWithPipelineMethod(StringBuilder sb)
    {
        sb.AppendLine("    private async ValueTask<TResponse> ExecuteWithPipeline<TRequest, TResponse>(");
        sb.AppendLine("        TRequest request,");
        sb.AppendLine("        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler,");
        sb.AppendLine("        List<PipelineEntry> pipelines,");
        sb.AppendLine("        CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        var orderedPipelines = pipelines.OrderBy(p => p.Order).ToList();");
        sb.AppendLine();
        sb.AppendLine("        // Execute Pre hooks");
        sb.AppendLine("        foreach (var entry in orderedPipelines.Where(e => e.Type == PipelineType.Pre))");
        sb.AppendLine("        {");
        sb.AppendLine("            var pre = (Func<TRequest, CancellationToken, ValueTask>)entry.Delegate;");
        sb.AppendLine("            await pre(request, ct);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Build Around chain (compose from innermost to outermost)");
        sb.AppendLine("        var arounds = orderedPipelines.Where(e => e.Type == PipelineType.Around).ToList();");
        sb.AppendLine();
        sb.AppendLine("        Func<ValueTask<TResponse>> next = () => handler(request, ct);");
        sb.AppendLine();
        sb.AppendLine("        for (int i = arounds.Count - 1; i >= 0; i--)");
        sb.AppendLine("        {");
        sb.AppendLine("            var around = (Func<TRequest, CancellationToken, Func<ValueTask<TResponse>>, ValueTask<TResponse>>)arounds[i].Delegate;");
        sb.AppendLine("            var currentNext = next;");
        sb.AppendLine("            next = () => around(request, ct, currentNext);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        TResponse response;");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            response = await next();");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Execute OnError hooks");
        sb.AppendLine("            foreach (var entry in orderedPipelines.Where(e => e.Type == PipelineType.OnError))");
        sb.AppendLine("            {");
        sb.AppendLine("                var onError = (Func<TRequest, Exception, CancellationToken, ValueTask>)entry.Delegate;");
        sb.AppendLine("                await onError(request, ex, ct);");
        sb.AppendLine("            }");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Execute Post hooks");
        sb.AppendLine("        foreach (var entry in orderedPipelines.Where(e => e.Type == PipelineType.Post))");
        sb.AppendLine("        {");
        sb.AppendLine("            var post = (Func<TRequest, TResponse, CancellationToken, ValueTask>)entry.Delegate;");
        sb.AppendLine("            await post(request, response, ct);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return response;");
        sb.AppendLine("    }");
    }

    private static void GenerateObjectSendMethod(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Sends a command and returns a response (object-based overload).");
        sb.AppendLine("    /// Note: Uses reflection. For best performance, use generic Send<TRequest, TResponse>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async ValueTask<object?> Send(object request, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        var requestType = request.GetType();");
        sb.AppendLine("        if (!_commandHandlers.TryGetValue(requestType, out var handlerDelegate))");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException($\"No handler registered for command type {requestType.Name}\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Invoke handler via reflection");
        sb.AppendLine("        var delegateType = handlerDelegate.GetType();");
        sb.AppendLine("        var invokeMethod = delegateType.GetMethod(\"Invoke\");");
        sb.AppendLine("        if (invokeMethod == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException(\"Could not find Invoke method on handler delegate\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var result = invokeMethod.Invoke(handlerDelegate, new object?[] { request, ct });");
        sb.AppendLine("        if (result is ValueTask<object> vtObj)");
        sb.AppendLine("        {");
        sb.AppendLine("            return await vtObj;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Handle generic ValueTask<TResponse>");
        sb.AppendLine("        // Note: This reflection-based path is only used for object overloads (opt-in)");
        sb.AppendLine("        // Regular generic Send<TRequest, TResponse> is zero-reflection");
        sb.AppendLine("        var resultType = result?.GetType();");
        sb.AppendLine("        if (resultType != null && resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(ValueTask<>))");
        sb.AppendLine("        {");
        sb.AppendLine("            var asTaskMethod = resultType.GetMethod(\"AsTask\");");
        sb.AppendLine("            if (asTaskMethod != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                var task = asTaskMethod.Invoke(result, null) as Task;");
        sb.AppendLine("                if (task != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    await task;");
        sb.AppendLine("                    return task.GetType().GetProperty(\"Result\")?.GetValue(task);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateObjectPublishMethod(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Publishes a notification to all registered handlers (object-based overload).");
        sb.AppendLine("    /// Note: Uses reflection. For best performance, use generic Publish<TNotification>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async ValueTask Publish(object notification, CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        var notificationType = notification.GetType();");
        sb.AppendLine("        if (!_notificationHandlers.TryGetValue(notificationType, out var handlers))");
        sb.AppendLine("        {");
        sb.AppendLine("            return; // No-op if no handlers");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        foreach (var handlerDelegate in handlers)");
        sb.AppendLine("        {");
        sb.AppendLine("            var invokeMethod = handlerDelegate.GetType().GetMethod(\"Invoke\");");
        sb.AppendLine("            if (invokeMethod != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                var result = invokeMethod.Invoke(handlerDelegate, new object?[] { notification, ct });");
        sb.AppendLine("                if (result is ValueTask vt)");
        sb.AppendLine("                {");
        sb.AppendLine("                    await vt;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateObjectStreamMethod(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Streams items from a stream request (object-based overload).");
        sb.AppendLine("    /// Note: Uses reflection. For best performance, use generic Stream<TRequest, TItem>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public async IAsyncEnumerable<object?> Stream(object request, [EnumeratorCancellation] CancellationToken ct = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        var requestType = request.GetType();");
        sb.AppendLine("        if (!_streamHandlers.TryGetValue(requestType, out var handlerDelegate))");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException($\"No stream handler registered for request type {requestType.Name}\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Invoke handler to get IAsyncEnumerable");
        sb.AppendLine("        var invokeMethod = handlerDelegate.GetType().GetMethod(\"Invoke\");");
        sb.AppendLine("        if (invokeMethod == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException(\"Could not find Invoke method on stream handler delegate\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var result = invokeMethod.Invoke(handlerDelegate, new object?[] { request, ct });");
        sb.AppendLine("        if (result == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Find IAsyncEnumerable<T> interface on result");
        sb.AppendLine("        var asyncEnumerableInterface = result.GetType().GetInterfaces()");
        sb.AppendLine("            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));");
        sb.AppendLine();
        sb.AppendLine("        if (asyncEnumerableInterface == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException(\"Handler result does not implement IAsyncEnumerable<T>\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Get GetAsyncEnumerator method from the interface");
        sb.AppendLine("        var getEnumeratorMethod = asyncEnumerableInterface.GetMethod(\"GetAsyncEnumerator\");");
        sb.AppendLine("        if (getEnumeratorMethod == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new InvalidOperationException(\"Could not find GetAsyncEnumerator method\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Get enumerator");
        sb.AppendLine("        var enumerator = getEnumeratorMethod.Invoke(result, new object[] { ct });");
        sb.AppendLine("        if (enumerator == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Get IAsyncEnumerator<T> interface");
        sb.AppendLine("        var asyncEnumeratorInterface = enumerator.GetType().GetInterfaces()");
        sb.AppendLine("            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>));");
        sb.AppendLine();
        sb.AppendLine("        if (asyncEnumeratorInterface == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var moveNextAsyncMethod = asyncEnumeratorInterface.GetMethod(\"MoveNextAsync\");");
        sb.AppendLine("        var currentProperty = asyncEnumeratorInterface.GetProperty(\"Current\");");
        sb.AppendLine();
        sb.AppendLine("        if (moveNextAsyncMethod == null || currentProperty == null)");
        sb.AppendLine("        {");
        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            while (true)");
        sb.AppendLine("            {");
        sb.AppendLine("                var moveNextResult = moveNextAsyncMethod.Invoke(enumerator, null);");
        sb.AppendLine("                if (moveNextResult is ValueTask<bool> vtBool)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (!await vtBool)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        break;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    yield return currentProperty.GetValue(enumerator);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    break;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            if (enumerator is IAsyncDisposable asyncDisposable)");
        sb.AppendLine("            {");
        sb.AppendLine("                await asyncDisposable.DisposeAsync();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                var disposeAsyncMethod = enumerator.GetType().GetMethod(\"DisposeAsync\", Type.EmptyTypes);");
        sb.AppendLine("                if (disposeAsyncMethod != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var disposeResult = disposeAsyncMethod.Invoke(enumerator, null);");
        sb.AppendLine("                    if (disposeResult is ValueTask vt)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        await vt;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static string GenerateBuilderFile(DispatcherConfig config, string visibility)
    {
        var sb = CreateFileHeader();
        AppendUsings(sb, "System", "System.Collections.Generic", "System.Threading", "System.Threading.Tasks");

        if (config.IncludeStreaming)
        {
            sb.AppendLine("using System.Runtime.CompilerServices;");
        }

        sb.AppendLine();
        AppendNamespaceAndClassHeader(sb, config.Namespace, visibility, config.Name);

        sb.AppendLine($"    {visibility} sealed class Builder : IDispatcherBuilder");
        sb.AppendLine("    {");
        sb.AppendLine($"        private readonly {config.Name} _dispatcher = new();");
        sb.AppendLine();

        // Command registration
        sb.AppendLine("        public Builder Command<TRequest, TResponse>(Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)");
        sb.AppendLine("        {");
        sb.AppendLine("            var requestType = typeof(TRequest);");
        sb.AppendLine("            if (_dispatcher._commandHandlers.ContainsKey(requestType))");
        sb.AppendLine("            {");
        sb.AppendLine("                throw new InvalidOperationException($\"Handler for {requestType.Name} already registered\");");
        sb.AppendLine("            }");
        sb.AppendLine("            _dispatcher._commandHandlers[requestType] = handler;");
        sb.AppendLine("            return this;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Notification registration
        sb.AppendLine("        public Builder Notification<TNotification>(Func<TNotification, CancellationToken, ValueTask> handler)");
        sb.AppendLine("        {");
        sb.AppendLine("            var notificationType = typeof(TNotification);");
        sb.AppendLine("            if (!_dispatcher._notificationHandlers.TryGetValue(notificationType, out var handlers))");
        sb.AppendLine("            {");
        sb.AppendLine("                handlers = new List<Delegate>();");
        sb.AppendLine("                _dispatcher._notificationHandlers[notificationType] = handlers;");
        sb.AppendLine("            }");
        sb.AppendLine("            handlers.Add(handler);");
        sb.AppendLine("            return this;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Stream registration
        if (config.IncludeStreaming)
        {
            sb.AppendLine("        public Builder Stream<TRequest, TItem>(Func<TRequest, CancellationToken, IAsyncEnumerable<TItem>> handler)");
            sb.AppendLine("        {");
            sb.AppendLine("            var requestType = typeof(TRequest);");
            sb.AppendLine("            if (_dispatcher._streamHandlers.ContainsKey(requestType))");
            sb.AppendLine("            {");
            sb.AppendLine("                throw new InvalidOperationException($\"Stream handler for {requestType.Name} already registered\");");
            sb.AppendLine("            }");
            sb.AppendLine("            _dispatcher._streamHandlers[requestType] = handler;");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Pipeline registration - Pre
        sb.AppendLine("        public Builder Pre<TRequest>(Func<TRequest, CancellationToken, ValueTask> pre, int order = 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var requestType = typeof(TRequest);");
        sb.AppendLine("            if (!_dispatcher._commandPipelines.TryGetValue(requestType, out var pipelines))");
        sb.AppendLine("            {");
        sb.AppendLine("                pipelines = new List<PipelineEntry>();");
        sb.AppendLine("                _dispatcher._commandPipelines[requestType] = pipelines;");
        sb.AppendLine("            }");
        sb.AppendLine("            pipelines.Add(new PipelineEntry { Type = PipelineType.Pre, Order = order, Delegate = pre });");
        sb.AppendLine("            return this;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Pipeline registration - Around
        sb.AppendLine("        public Builder Around<TRequest, TResponse>(Func<TRequest, CancellationToken, Func<ValueTask<TResponse>>, ValueTask<TResponse>> around, int order = 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var requestType = typeof(TRequest);");
        sb.AppendLine("            if (!_dispatcher._commandPipelines.TryGetValue(requestType, out var pipelines))");
        sb.AppendLine("            {");
        sb.AppendLine("                pipelines = new List<PipelineEntry>();");
        sb.AppendLine("                _dispatcher._commandPipelines[requestType] = pipelines;");
        sb.AppendLine("            }");
        sb.AppendLine("            pipelines.Add(new PipelineEntry { Type = PipelineType.Around, Order = order, Delegate = around });");
        sb.AppendLine("            return this;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Pipeline registration - Post
        sb.AppendLine("        public Builder Post<TRequest, TResponse>(Func<TRequest, TResponse, CancellationToken, ValueTask> post, int order = 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var requestType = typeof(TRequest);");
        sb.AppendLine("            if (!_dispatcher._commandPipelines.TryGetValue(requestType, out var pipelines))");
        sb.AppendLine("            {");
        sb.AppendLine("                pipelines = new List<PipelineEntry>();");
        sb.AppendLine("                _dispatcher._commandPipelines[requestType] = pipelines;");
        sb.AppendLine("            }");
        sb.AppendLine("            pipelines.Add(new PipelineEntry { Type = PipelineType.Post, Order = order, Delegate = post });");
        sb.AppendLine("            return this;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Pipeline registration - OnError
        sb.AppendLine("        public Builder OnError<TRequest, TResponse>(Func<TRequest, Exception, CancellationToken, ValueTask> onError, int order = 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            var requestType = typeof(TRequest);");
        sb.AppendLine("            if (!_dispatcher._commandPipelines.TryGetValue(requestType, out var pipelines))");
        sb.AppendLine("            {");
        sb.AppendLine("                pipelines = new List<PipelineEntry>();");
        sb.AppendLine("                _dispatcher._commandPipelines[requestType] = pipelines;");
        sb.AppendLine("            }");
        sb.AppendLine("            pipelines.Add(new PipelineEntry { Type = PipelineType.OnError, Order = order, Delegate = onError });");
        sb.AppendLine("            return this;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Stream pipeline registration - PreStream
        if (config.IncludeStreaming)
        {
            sb.AppendLine("        public Builder PreStream<TRequest>(Func<TRequest, CancellationToken, ValueTask> pre, int order = 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            var requestType = typeof(TRequest);");
            sb.AppendLine("            if (!_dispatcher._streamPipelines.TryGetValue(requestType, out var pipelines))");
            sb.AppendLine("            {");
            sb.AppendLine("                pipelines = new List<PipelineEntry>();");
            sb.AppendLine("                _dispatcher._streamPipelines[requestType] = pipelines;");
            sb.AppendLine("            }");
            sb.AppendLine("            pipelines.Add(new PipelineEntry { Type = PipelineType.Pre, Order = order, Delegate = pre });");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Module registration
        sb.AppendLine("        public Builder AddModule(IModule module)");
        sb.AppendLine("        {");
        sb.AppendLine("            module.Register(this);");
        sb.AppendLine("            return this;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Build method
        sb.AppendLine($"        public {config.Name} Build() => _dispatcher;");
        sb.AppendLine();

        // IDispatcherBuilder implementation
        sb.AppendLine("        // Explicit interface implementations");
        sb.AppendLine("        IDispatcherBuilder IDispatcherBuilder.Command<TRequest, TResponse>(Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)");
        sb.AppendLine("            => Command(handler);");
        sb.AppendLine();
        sb.AppendLine("        IDispatcherBuilder IDispatcherBuilder.Notification<TNotification>(Func<TNotification, CancellationToken, ValueTask> handler)");
        sb.AppendLine("            => Notification(handler);");
        sb.AppendLine();

        if (config.IncludeStreaming)
        {
            sb.AppendLine("        IDispatcherBuilder IDispatcherBuilder.Stream<TRequest, TItem>(Func<TRequest, CancellationToken, IAsyncEnumerable<TItem>> handler)");
            sb.AppendLine("            => Stream(handler);");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateContractsFile(DispatcherConfig config, string visibility)
    {
        var sb = CreateFileHeader();
        AppendUsings(sb, "System.Collections.Generic", "System.Threading", "System.Threading.Tasks");

        sb.AppendLine($"namespace {config.Namespace};");
        sb.AppendLine();

        // IModule interface
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Interface for modular registration of handlers.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"{visibility} interface IModule");
        sb.AppendLine("{");
        sb.AppendLine("    void Register(IDispatcherBuilder builder);");
        sb.AppendLine("}");
        sb.AppendLine();

        // IDispatcherBuilder interface
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Builder interface for registering handlers.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"{visibility} interface IDispatcherBuilder");
        sb.AppendLine("{");
        sb.AppendLine("    IDispatcherBuilder Command<TRequest, TResponse>(System.Func<TRequest, CancellationToken, ValueTask<TResponse>> handler);");
        sb.AppendLine("    IDispatcherBuilder Notification<TNotification>(System.Func<TNotification, CancellationToken, ValueTask> handler);");

        if (config.IncludeStreaming)
        {
            sb.AppendLine("    IDispatcherBuilder Stream<TRequest, TItem>(System.Func<TRequest, CancellationToken, IAsyncEnumerable<TItem>> handler);");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Command handler interface
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Handler for a command that returns a response.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"{visibility} interface ICommandHandler<TRequest, TResponse>");
        sb.AppendLine("{");
        sb.AppendLine("    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Notification handler interface
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Handler for a notification.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"{visibility} interface INotificationHandler<TNotification>");
        sb.AppendLine("{");
        sb.AppendLine("    ValueTask Handle(TNotification notification, CancellationToken ct);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Stream handler interface
        if (config.IncludeStreaming)
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Handler for a stream request.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"{visibility} interface IStreamHandler<TRequest, TItem>");
            sb.AppendLine("{");
            sb.AppendLine("    IAsyncEnumerable<TItem> Handle(TRequest request, CancellationToken ct);");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Command pipeline delegates
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Delegate for invoking the next command handler in the pipeline.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"{visibility} delegate ValueTask<TResponse> CommandNext<TResponse>();");
        sb.AppendLine();

        // Command pipeline interface
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pipeline for command handling.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"{visibility} interface ICommandPipeline<TRequest, TResponse>");
        sb.AppendLine("{");
        sb.AppendLine("    ValueTask Pre(TRequest request, CancellationToken ct);");
        sb.AppendLine("    ValueTask<TResponse> Around(TRequest request, CancellationToken ct, CommandNext<TResponse> next);");
        sb.AppendLine("    ValueTask Post(TRequest request, TResponse response, CancellationToken ct);");
        sb.AppendLine("    ValueTask OnError(TRequest request, System.Exception ex, CancellationToken ct);");
        sb.AppendLine("}");
        sb.AppendLine();

        if (config.IncludeStreaming)
        {
            // Stream pipeline delegates
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Delegate for invoking the next stream handler in the pipeline.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"{visibility} delegate IAsyncEnumerable<TItem> StreamNext<TItem>();");
            sb.AppendLine();

            // Stream pipeline interface
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Pipeline for stream handling.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine($"{visibility} interface IStreamPipeline<TRequest, TItem>");
            sb.AppendLine("{");
            sb.AppendLine("    ValueTask Pre(TRequest request, CancellationToken ct);");
            sb.AppendLine("    IAsyncEnumerable<TItem> Around(TRequest request, CancellationToken ct, StreamNext<TItem> next);");
            sb.AppendLine("    ValueTask Post(TRequest request, CancellationToken ct);");
            sb.AppendLine("    ValueTask OnError(TRequest request, System.Exception ex, CancellationToken ct);");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static void ReportDiagnostic(
        SourceProductionContext spc,
        string id,
        string message,
        DiagnosticSeverity severity,
        Location location)
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            message,
            message,
            "PatternKit.Messaging",
            severity,
            isEnabledByDefault: true);

        spc.ReportDiagnostic(Diagnostic.Create(descriptor, location));
    }

    private static StringBuilder CreateFileHeader()
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        return sb;
    }

    private static void AppendUsings(StringBuilder sb, params string[] usings)
    {
        foreach (var ns in usings)
        {
            sb.AppendLine($"using {ns};");
        }
        sb.AppendLine();
    }

    private static void AppendNamespaceAndClassHeader(StringBuilder sb, string ns, string visibility, string className)
    {
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"{visibility} sealed partial class {className}");
        sb.AppendLine("{");
    }

    private sealed class DispatcherConfig
    {
        public string Namespace { get; set; } = "Generated.Messaging";
        public string Name { get; set; } = "AppDispatcher";
        public bool IncludeObjectOverloads { get; set; }
        public bool IncludeStreaming { get; set; } = true;
        public int Visibility { get; set; }
    }
}
