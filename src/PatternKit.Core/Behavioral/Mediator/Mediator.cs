using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.Mediator;

/// <summary>
/// Allocation-light mediator supporting commands (request/response), notifications (fan-out),
/// and streaming commands. Provides global <c>Pre</c>, <c>Whole</c>, and <c>Post</c> behaviors which
/// compose similarly to pipeline behaviors in libraries like MediatR, but remain allocation-light by
/// operating over boxed <see cref="object"/> values.
/// </summary>
/// <remarks>
/// Behavior ordering:
/// <list type="number">
/// <item><description>All registered <see cref="Builder.Pre(PreBehavior)"/> behaviors execute in registration order before any handler / whole behaviors.</description></item>
/// <item><description><see cref="Builder.Whole(WholeBehavior)"/> behaviors are wrapped inside-out (last registered is innermost, closest to the handler).</description></item>
/// <item><description>The concrete command / stream handler executes.</description></item>
/// <item><description>All registered <see cref="Builder.Post(PostBehavior)"/> behaviors execute in registration order after handler completion (or after stream enumeration completes).</description></item>
/// </list>
/// Notifications do not execute <c>Whole</c> behaviors by design (they are fire-and-forget fan-out) but still receive <c>Pre</c> and <c>Post</c> behaviors.
/// </remarks>
public sealed class Mediator // mark partial to allow future surface extension without large file edits
{
    /// <summary>
    /// A pre behavior invoked before a handler (command, notification, or stream) executes.
    /// May perform validation, logging, metrics, or throw to short-circuit the pipeline.
    /// </summary>
    /// <param name="request">The boxed request/notification instance.</param>
    /// <param name="ct">Cancellation token.</param>
    public delegate ValueTask PreBehavior(in object? request, CancellationToken ct);

    /// <summary>
    /// A post behavior invoked after a handler (or after a stream completes enumeration).
    /// </summary>
    /// <param name="request">The original boxed request/notification.</param>
    /// <param name="response">The boxed response for commands (null for notifications / streams).</param>
    /// <param name="ct">Cancellation token.</param>
    public delegate ValueTask PostBehavior(in object? request, object? response, CancellationToken ct);

    /// <summary>
    /// Delegate representing the next continuation in a composed <see cref="WholeBehavior"/> chain.
    /// </summary>
    public delegate ValueTask<object?> MediatorNext(in object? request, CancellationToken ct);

    /// <summary>
    /// A whole behavior wraps the handler and subsequent behaviors, allowing around advice (logging timing, retry, exception decoration, etc.).
    /// </summary>
    /// <param name="request">The boxed request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="next">Continuation that invokes the next behavior or the underlying handler.</param>
    public delegate ValueTask<object?> WholeBehavior(in object? request, CancellationToken ct, MediatorNext next);

    /// <summary>
    /// Runtime-adapted command handler (boxed signature) produced by the builder from typed registrations.
    /// </summary>
    public delegate ValueTask<object?> CommandHandler(in object? request, CancellationToken ct);

    /// <summary>
    /// Runtime-adapted notification handler (boxed signature) produced by the builder from typed registrations.
    /// </summary>
    public delegate ValueTask NotificationHandler(in object notification, CancellationToken ct);

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Runtime-adapted stream handler (boxed signature) producing an async sequence of boxed items.
    /// </summary>
    public delegate IAsyncEnumerable<object?> StreamHandler(in object? request, CancellationToken ct);
#endif

    private readonly ReadOnlyDictionary<Type, CommandHandler> _commands;
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    private readonly ReadOnlyDictionary<Type, StreamHandler> _streams;
#endif
    private readonly ReadOnlyDictionary<Type, NotificationHandler[]> _notifications;
    private readonly PreBehavior[] _pre;
    private readonly PostBehavior[] _post;
    private readonly WholeBehavior[] _whole;

    private Mediator(
        ReadOnlyDictionary<Type, CommandHandler> commands,
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        ReadOnlyDictionary<Type, StreamHandler> streams,
#endif
        ReadOnlyDictionary<Type, NotificationHandler[]> notifications,
        PreBehavior[] pre,
        PostBehavior[] post,
        WholeBehavior[] whole)
    {
        _commands = commands;
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        _streams = streams;
#endif
        _notifications = notifications;
        _pre = pre;
        _post = post;
        _whole = whole;
    }

    /// <summary>Create a new builder for a mediator.</summary>
    public static Builder Create() => new();

    // -------------- Public API --------------

    /// <summary>
    /// Send a typed request and receive a typed response via a single registered command handler.
    /// </summary>
    /// <typeparam name="TRequest">Concrete request type.</typeparam>
    /// <typeparam name="TResponse">Expected response type.</typeparam>
    /// <param name="request">The request value (passed by readonly reference to avoid copying large structs).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler produced response.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler exists or the result cannot be cast to <typeparamref name="TResponse"/>.</exception>
    public ValueTask<TResponse?> Send<TRequest, TResponse>(in TRequest request, CancellationToken ct = default)
        => Core<TRequest, TResponse>(request, ct);

    private async ValueTask<TResponse?> Core<TRequest, TResponse>(TRequest request, CancellationToken ct)
    {
        if (!_commands.TryGetValue(typeof(TRequest), out var handler))
            throw new InvalidOperationException($"No command handler registered for request type '{typeof(TRequest)}'.");

        object? boxed = request;
        foreach (var b in _pre)
            await b(in boxed, ct).ConfigureAwait(false);

        MediatorNext next = (in req, ct2) => handler(in req, ct2);
        for (var i = _whole.Length - 1; i >= 0; i--)
        {
            var w = _whole[i];
            var prev = next;
            next = (in req, ct2) => w(in req, ct2, prev);
        }

        var obj = await next(in boxed, ct).ConfigureAwait(false);

        foreach (var b in _post)
            await b(in boxed, obj, ct).ConfigureAwait(false);

        return obj switch
        {
            TResponse r => r,
            null => default,
            _ => throw new InvalidOperationException(
                $"Handler returned incompatible result for '{typeof(TRequest)}' -> expected '{typeof(TResponse)}', got '{obj.GetType()}'.")
        };
    }

    /// <summary>
    /// Publish a notification to all registered handlers. Absence of handlers is a no-op (never throws).
    /// </summary>
    /// <typeparam name="TNotification">Notification type.</typeparam>
    /// <param name="notification">Notification instance (by readonly reference).</param>
    /// <param name="ct">Cancellation token.</param>
    public ValueTask Publish<TNotification>(in TNotification notification, CancellationToken ct = default)
        => PublishCore(notification, ct);

    private async ValueTask PublishCore<TNotification>(TNotification notification, CancellationToken ct)
    {
        if (!_notifications.TryGetValue(typeof(TNotification), out var handlers) || handlers.Length == 0)
            return; // no-op

        object boxed = notification!;
        foreach (var b in _pre)
            await b(in boxed, ct).ConfigureAwait(false);

        // We do not wrap notifications with Whole behaviors to keep semantics simple; they are fire-and-forget fan-out.
        foreach (var h in handlers)
            await h(in boxed, ct).ConfigureAwait(false);

        foreach (var b in _post)
            await b(in boxed, null, ct).ConfigureAwait(false);
    }

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Execute a streaming command, yielding items lazily. <c>Pre</c> behaviors run before enumeration starts, <c>Post</c> behaviors after completion.
    /// </summary>
    /// <typeparam name="TRequest">Stream request type.</typeparam>
    /// <typeparam name="TItem">Item element type.</typeparam>
    /// <param name="request">Request instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async sequence of <typeparamref name="TItem"/>.</returns>
    public IAsyncEnumerable<TItem> Stream<TRequest, TItem>(in TRequest request, CancellationToken ct = default)
        => StreamCore<TRequest, TItem>(request, ct);

    private async IAsyncEnumerable<TItem> StreamCore<TRequest, TItem>(
        TRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_streams.TryGetValue(typeof(TRequest), out var handler))
            throw new InvalidOperationException($"No stream handler registered for request type '{typeof(TRequest)}'.");

        object boxed = request!;
        foreach (var b in _pre)
            await b(in boxed, ct).ConfigureAwait(false);

        // Wrap the stream handler as a whole operation producing a BoxedAsyncEnumerable
        MediatorNext next = (in req, ct2) => new ValueTask<object?>(new BoxedAsyncEnumerable(handler(in req, ct2)));
        for (var i = _whole.Length - 1; i >= 0; i--)
        {
            var w = _whole[i];
            var prev = next;
            next = (in req, ct2) => w(in req, ct2, prev);
        }

        var produced = _whole.Length == 0
            ? new BoxedAsyncEnumerable(handler(in boxed, ct))
            : await next(in boxed, ct).ConfigureAwait(false);

        var seq = produced switch
        {
            BoxedAsyncEnumerable bae => bae.Inner,
            IAsyncEnumerable<object?> raw => raw,
            _ => handler(in boxed, ct)
        };

        await foreach (var item in seq.ConfigureAwait(false).WithCancellation(ct))
        {
            if (item is TItem ti) yield return ti;
            else if (item is null && default(TItem) is null) yield return default!;
            else throw new InvalidOperationException($"Stream item type mismatch. Expected '{typeof(TItem)}' but got '{item?.GetType()}'.");
        }

        // After stream completes
        foreach (var b in _post)
            await b(in boxed, null, ct).ConfigureAwait(false);
    }

    private readonly struct BoxedAsyncEnumerable
    {
        public readonly IAsyncEnumerable<object?> Inner;
        public BoxedAsyncEnumerable(IAsyncEnumerable<object?> inner) => Inner = inner;
    }
#endif

    // -------------- Builder --------------

    public sealed class Builder
    {
        /// <summary>
        /// Fluent builder used to register handlers and behaviors before creating an immutable <see cref="Mediator"/> instance.
        /// Not thread-safe; configure then call <see cref="Build"/>.
        /// </summary>
        // Typed delegate shapes for registration (with 'in' parameters)
        public delegate ValueTask<TResponse> CommandHandlerTyped<TRequest, TResponse>(in TRequest request, CancellationToken ct);

        public delegate TResponse SyncCommandHandlerTyped<TRequest, TResponse>(in TRequest request);

        public delegate ValueTask NotificationHandlerTyped<TNotification>(in TNotification notification, CancellationToken ct);

        public delegate void SyncNotificationHandlerTyped<TNotification>(in TNotification notification);
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        public delegate IAsyncEnumerable<TItem> StreamHandlerTyped<TRequest, TItem>(in TRequest request, CancellationToken ct);
#endif

        private readonly Dictionary<Type, CommandHandler> _commands = new();
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        private readonly Dictionary<Type, StreamHandler> _streams = new();
#endif
        private readonly Dictionary<Type, List<NotificationHandler>> _notifications = new();
        private readonly List<PreBehavior> _pre = new(4);
        private readonly List<PostBehavior> _post = new(4);
        private readonly List<WholeBehavior> _whole = new(4);

        /// <summary>Add a pre behavior executed before any handler logic.</summary>
        public Builder Pre(PreBehavior behavior)
        {
            if (behavior is not null) _pre.Add(behavior);
            return this;
        }

        /// <summary>Add a post behavior executed after handler or stream completion.</summary>
        public Builder Post(PostBehavior behavior)
        {
            if (behavior is not null) _post.Add(behavior);
            return this;
        }

        /// <summary>Add a whole (around) behavior wrapping handler + remaining behaviors.</summary>
        public Builder Whole(WholeBehavior behavior)
        {
            if (behavior is not null) _whole.Add(behavior);
            return this;
        }

        /// <summary>Register an asynchronous command handler.</summary>
        public Builder Command<TRequest, TResponse>(CommandHandlerTyped<TRequest, TResponse> handler)
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));

            _commands[typeof(TRequest)] = Adapt;
            return this;

            ValueTask<object?> Adapt(in object? req, CancellationToken ct)
                => req is not TRequest r 
                    ? throw new InvalidOperationException($"Invalid request type for '{typeof(TRequest)}'.") 
                    : MediatorHelpers.Box(handler(in r, ct));
        }

        /// <summary>Register a synchronous command handler (wrapped internally).</summary>
        public Builder Command<TRequest, TResponse>(SyncCommandHandlerTyped<TRequest, TResponse> handler)
            => Command<TRequest, TResponse>((in r, _) => new ValueTask<TResponse>(handler(in r)));

        /// <summary>Register an asynchronous notification handler.</summary>
        public Builder Notification<TNotification>(NotificationHandlerTyped<TNotification> handler)
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));

            ValueTask Adapt(in object n, CancellationToken ct)
                => n is TNotification t
                    ? handler(in t, ct)
                    : throw new InvalidOperationException($"Invalid notification type for '{typeof(TNotification)}'.");

            if (!_notifications.TryGetValue(typeof(TNotification), out var list))
                _notifications[typeof(TNotification)] = list = new List<NotificationHandler>(2);
            list.Add(Adapt);
            return this;
        }

        /// <summary>Register a synchronous notification handler (wrapped internally).</summary>
        public Builder Notification<TNotification>(SyncNotificationHandlerTyped<TNotification> handler)
            => Notification<TNotification>((in n, _) =>
            {
                handler(in n);
                return default;
            });

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        /// <summary>Register a streaming handler.</summary>
        public Builder Stream<TRequest, TItem>(StreamHandlerTyped<TRequest, TItem> handler)
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));

            _streams[typeof(TRequest)] = Adapt;
            return this;

            IAsyncEnumerable<object?> Adapt(in object? req, CancellationToken ct)
                => req is TRequest r
                    ? AdaptEnum(handler(in r, ct), ct)
                    : throw new InvalidOperationException($"Invalid request type for stream '{typeof(TRequest)}'.");

            static async IAsyncEnumerable<object?> AdaptEnum(
                IAsyncEnumerable<TItem> items, 
                [EnumeratorCancellation] CancellationToken ct)
            {
                await foreach (var it in items.ConfigureAwait(false).WithCancellation(ct))
                    yield return it;
            }
        }
#endif
        /// <summary>Finalize registrations and build an immutable mediator instance.</summary>
        public Mediator Build()
        {
            var cmds = new ReadOnlyDictionary<Type, CommandHandler>(_commands);
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
            var streams = new ReadOnlyDictionary<Type, StreamHandler>(_streams);
#endif
            var notes = new Dictionary<Type, NotificationHandler[]>(_notifications.Count);
            foreach (var kvp in _notifications)
                notes[kvp.Key] = kvp.Value.ToArray();
            return new Mediator(
                cmds,
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
                streams,
#endif
                new ReadOnlyDictionary<Type, NotificationHandler[]>(notes),
                _pre.ToArray(),
                _post.ToArray(),
                _whole.ToArray());
        }
    }
}

// NOTE: We inserted documentation; actual logic for Command<TRequest,TResponse> remains above. The stub above prevents duplication.
// DocFX will merge comments with the original method body.

/// <summary>
/// Helper extensions for adapting <see cref="Task{TResult}"/> to <see cref="ValueTask{TResult}"/> in performance-sensitive paths.
/// </summary>
internal static class TaskExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> AsValueTask<T>(this Task<T> task) => new(task);
}

/// <summary>
/// Internal helpers for boxing/unboxing generic <see cref="ValueTask{TResult}"/> efficiently without additional allocations.
/// </summary>
internal static class MediatorHelpers
{
    /// <summary>
    /// Box a <see cref="ValueTask{TResult}"/> without forcing an allocation in the successful synchronous completion path.
    /// </summary>
    /// <typeparam name="T">Underlying result type.</typeparam>
    /// <param name="vt">Original value task.</param>
    /// <returns>Boxed value task returning an <see cref="object"/> or <c>null</c>.</returns>
    public static ValueTask<object?> Box<T>(ValueTask<T> vt)
    {
        if (vt.IsCompletedSuccessfully) return new ValueTask<object?>(vt.Result);
        return Await(vt);

        static async ValueTask<object?> Await(ValueTask<T> v)
        {
            var r = await v.ConfigureAwait(false);
            return r;
        }
    }
}