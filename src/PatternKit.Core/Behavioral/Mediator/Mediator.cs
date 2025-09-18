using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PatternKit.Behavioral.Mediator;

/// <summary>
/// Allocation-light mediator supporting commands (request/response), notifications (fan-out),
/// and streaming commands. Provides global pre/post/whole behaviors and sync/async adapters.
/// </summary>
public sealed class Mediator
{
    // Behaviors are untyped by design to keep the core simple and avoid per-closed-generic registries.
    // We adapt typed handlers to object-based delegates once at build time.

    public delegate ValueTask PreBehavior(in object request, CancellationToken ct);
    public delegate ValueTask PostBehavior(in object request, object? response, CancellationToken ct);

    public delegate ValueTask<object?> MediatorNext(in object request, CancellationToken ct);
    public delegate ValueTask<object?> WholeBehavior(in object request, CancellationToken ct, MediatorNext next);

    // Core handler shapes (object-typed at runtime to avoid open generic storage after build)
    public delegate ValueTask<object?> CommandHandler(in object request, CancellationToken ct);
    public delegate ValueTask NotificationHandler(in object notification, CancellationToken ct);
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    public delegate IAsyncEnumerable<object?> StreamHandler(in object request, CancellationToken ct);
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

    /// <summary>Send a request and receive a response via a single handler.</summary>
    public ValueTask<TResponse> Send<TRequest, TResponse>(in TRequest request, CancellationToken ct = default)
        => Core<TRequest, TResponse>(request, ct);

    private async ValueTask<TResponse> Core<TRequest, TResponse>(TRequest request, CancellationToken ct)
    {
        if (!_commands.TryGetValue(typeof(TRequest), out var handler))
            throw new InvalidOperationException($"No command handler registered for request type '{typeof(TRequest)}'.");

        object boxed = request;
        foreach (var b in _pre)
            await b(in boxed, ct).ConfigureAwait(false);

        MediatorNext next = (in object req, CancellationToken ct2) => handler(in req, ct2);
        for (int i = _whole.Length - 1; i >= 0; i--)
        {
            var w = _whole[i];
            var prev = next;
            next = (in object req, CancellationToken ct2) => w(in req, ct2, prev);
        }

        var obj = await next(in boxed, ct).ConfigureAwait(false);

        foreach (var b in _post)
            await b(in boxed, obj, ct).ConfigureAwait(false);

        if (obj is TResponse r) return r;
        if (obj is null) return default(TResponse);
        throw new InvalidOperationException($"Handler returned incompatible result for '{typeof(TRequest)}' -> expected '{typeof(TResponse)}', got '{obj.GetType()}'.");
    }

    /// <summary>Publish a notification to all registered handlers.</summary>
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
    /// <summary>Execute a streaming command, yielding items from the handler.</summary>
    public IAsyncEnumerable<TItem> Stream<TRequest, TItem>(in TRequest request, CancellationToken ct = default)
        => StreamCore<TRequest, TItem>(request, ct);

    private async IAsyncEnumerable<TItem> StreamCore<TRequest, TItem>(TRequest request, CancellationToken ct)
    {
        if (!_streams.TryGetValue(typeof(TRequest), out var handler))
            throw new InvalidOperationException($"No stream handler registered for request type '{typeof(TRequest)}'.");

        object boxed = request!;
        foreach (var b in _pre)
            await b(in boxed, ct).ConfigureAwait(false);

        // Wrap the stream handler as a whole operation producing a BoxedAsyncEnumerable
        MediatorNext next = (in object req, CancellationToken ct2) => new ValueTask<object?>(new BoxedAsyncEnumerable(handler(in req, ct2)));
        for (int i = _whole.Length - 1; i >= 0; i--)
        {
            var w = _whole[i];
            var prev = next;
            next = (in object req, CancellationToken ct2) => w(in req, ct2, prev);
        }

        var produced = _whole.Length == 0
            ? (object?)new BoxedAsyncEnumerable(handler(in boxed, ct))
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

        // ---- Behaviors ----
        public Builder Pre(PreBehavior behavior) { if (behavior is not null) _pre.Add(behavior); return this; }
        public Builder Post(PostBehavior behavior) { if (behavior is not null) _post.Add(behavior); return this; }
        public Builder Whole(WholeBehavior behavior) { if (behavior is not null) _whole.Add(behavior); return this; }

        // ---- Commands ----
        public Builder Command<TRequest, TResponse>(CommandHandlerTyped<TRequest, TResponse> handler)
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            ValueTask<object?> Adapt(in object req, CancellationToken ct)
            {
                if (req is not TRequest r)
                    throw new InvalidOperationException($"Invalid request type for '{typeof(TRequest)}'.");
                return MediatorHelpers.Box<TResponse>(handler(in r, ct));
            }
            _commands[typeof(TRequest)] = Adapt;
            return this;
        }

        public Builder Command<TRequest, TResponse>(SyncCommandHandlerTyped<TRequest, TResponse> handler)
            => Command<TRequest, TResponse>((in TRequest r, CancellationToken _) => new ValueTask<TResponse>(handler(in r)));

        // ---- Notifications ----
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

        public Builder Notification<TNotification>(SyncNotificationHandlerTyped<TNotification> handler)
            => Notification<TNotification>((in TNotification n, CancellationToken _) => { handler(in n); return default; });

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
        // ---- Streams ----
        public Builder Stream<TRequest, TItem>(StreamHandlerTyped<TRequest, TItem> handler)
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));
            IAsyncEnumerable<object?> Adapt(in object req, CancellationToken ct)
                => req is TRequest r
                    ? AdaptEnum(handler(in r, ct), ct)
                    : throw new InvalidOperationException($"Invalid request type for stream '{typeof(TRequest)}'.");

            _streams[typeof(TRequest)] = Adapt;
            return this;

            static async IAsyncEnumerable<object?> AdaptEnum(IAsyncEnumerable<TItem> items, CancellationToken ct)
            {
                await foreach (var it in items.ConfigureAwait(false).WithCancellation(ct))
                    yield return it;
            }
        }
#endif

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

internal static class TaskExtensions
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> AsValueTask<T>(this Task<T> task) => new(task);
}

internal static class MediatorHelpers
{
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
