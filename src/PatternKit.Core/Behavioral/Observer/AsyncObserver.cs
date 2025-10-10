using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.Observer;

/// <summary>
/// Async Observer (typed, fluent, thread-safe)
/// Asynchronously notifies multiple observers about events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation mirrors <see cref="Observer{TEvent}"/> but for asynchronous handlers. It uses a copy-on-write array
/// for subscriptions, providing lock-free add/remove and snapshot-based iteration during publish. Handlers can optionally be
/// gated by a predicate that is awaited. Error handling behavior is configured via the fluent <see cref="Builder"/>.
/// </para>
/// <para>After <see cref="Create"/> and <see cref="Builder.Build"/>, the observer instance is immutable and thread-safe.</para>
/// </remarks>
/// <typeparam name="TEvent">The event payload type to broadcast.</typeparam>
public sealed class AsyncObserver<TEvent>
{
    /// <summary>
    /// Asynchronous predicate to filter whether a handler should receive the event.
    /// </summary>
    /// <param name="evt">The event.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is <see langword="true"/> to invoke the handler; otherwise <see langword="false"/>.
    /// </returns>
    public delegate ValueTask<bool> Predicate(TEvent evt);

    /// <summary>
    /// Asynchronous handler invoked when a published event passes an optional predicate.
    /// </summary>
    /// <param name="evt">The event payload.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when handling finishes.</returns>
    public delegate ValueTask Handler(TEvent evt);

    /// <summary>
    /// Asynchronous error sink invoked when a handler throws; never throws back into the publisher.
    /// </summary>
    /// <param name="ex">The exception from a handler.</param>
    /// <param name="evt">The event being processed.</param>
    /// <returns>A <see cref="ValueTask"/> representing sink completion.</returns>
    public delegate ValueTask ErrorSink(Exception ex, TEvent evt);

    private enum ErrorPolicy
    {
        Swallow,
        ThrowFirst,
        ThrowAggregate
    }

    private readonly ErrorPolicy _errorPolicy;
    private readonly ErrorSink? _errorSink;

    private Entry[] _entries = []; // copy-on-write storage
    private int _nextId;

    private struct Entry
    {
        public int Id;
        public Predicate? Pred;
        public Handler Callback;
    }

    private AsyncObserver(ErrorPolicy policy, ErrorSink? sink)
        => (_errorPolicy, _errorSink) = (policy, sink);

    /// <summary>
    /// Current number of active subscriptions (approximate during concurrent updates).
    /// </summary>
    public int SubscriberCount => Volatile.Read(ref _entries).Length;

    /// <summary>
    /// Publish an event to all matching subscribers asynchronously. Exceptions are handled according to the configured policy.
    /// </summary>
    /// <param name="evt">The event value.</param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <exception cref="AggregateException">When policy is ThrowAggregate and one or more handlers threw.</exception>
    /// <exception cref="Exception">When policy is ThrowFirst and a handler threw; the first exception is rethrown.</exception>
    /// <exception cref="OperationCanceledException">If <paramref name="cancellationToken"/> is canceled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask PublishAsync(TEvent evt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Volatile.Read(ref _entries);
        List<Exception>? errors = null;

        foreach (var t in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var e = t; // copy to avoid ref across await
            var run = e.Pred is null || await e.Pred(evt).ConfigureAwait(false);
            if (!run) continue;

            try
            {
                await e.Callback(evt).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var sink = _errorSink;
                if (sink is not null)
                {
                    try
                    {
                        await sink(ex, evt).ConfigureAwait(false);
                    }
                    catch
                    {
                        /* swallow */
                    }
                }

                switch (_errorPolicy)
                {
                    case ErrorPolicy.Swallow:
                        break;
                    case ErrorPolicy.ThrowFirst:
                        throw;
                    case ErrorPolicy.ThrowAggregate:
                        (errors ??= []).Add(ex);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        if (errors is { Count: > 0 }) throw new AggregateException(errors);
    }

    // Subscriptions
    /// <summary>
    /// Subscribe an asynchronous handler to receive all events.
    /// </summary>
    /// <param name="handler">The handler to invoke.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Handler handler) => Subscribe(null, handler);

    /// <summary>
    /// Subscribe an asynchronous handler with an optional asynchronous predicate filter.
    /// </summary>
    /// <param name="predicate">The filter to decide whether to deliver the event to the handler (optional).</param>
    /// <param name="handler">The handler to invoke.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Predicate? predicate, Handler handler)
    {
        var id = Interlocked.Increment(ref _nextId);
        while (true)
        {
            var curr = Volatile.Read(ref _entries);
            var next = new Entry[curr.Length + 1];
            Array.Copy(curr, next, curr.Length);
            next[curr.Length] = new Entry { Id = id, Pred = predicate, Callback = handler };
            if (Interlocked.CompareExchange(ref _entries, next, curr) == curr)
                break;
        }

        return new Subscription(this, id);
    }

    // Convenience adapters for sync delegates
    /// <summary>
    /// Convenience adapter: subscribe a synchronous <see cref="Observer{TEvent}.Handler"/> to the async observer.
    /// </summary>
    /// <param name="handler">The synchronous handler to adapt.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Observer<TEvent>.Handler handler)
        => Subscribe(null, e =>
        {
            handler(in e);
            return default;
        });

    /// <summary>
    /// Convenience adapter: subscribe a synchronous predicate and handler to the async observer.
    /// </summary>
    /// <param name="predicate">The synchronous predicate to filter events.</param>
    /// <param name="handler">The synchronous handler to adapt.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Observer<TEvent>.Predicate predicate, Observer<TEvent>.Handler handler)
        => Subscribe((Predicate)(e => new ValueTask<bool>(predicate(in e))),
            e =>
            {
                handler(in e);
                return default;
            });

    private void Unsubscribe(int id)
    {
        while (true)
        {
            var curr = Volatile.Read(ref _entries);
            var idx = -1;
            for (var i = 0; i < curr.Length; i++)
                if (curr[i].Id == id)
                {
                    idx = i;
                    break;
                }

            if (idx < 0) return;

            var next = new Entry[curr.Length - 1];
            if (idx > 0) Array.Copy(curr, 0, next, 0, idx);
            if (idx < curr.Length - 1) Array.Copy(curr, idx + 1, next, idx, curr.Length - idx - 1);
            if (Interlocked.CompareExchange(ref _entries, next, curr) == curr)
                return;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private AsyncObserver<TEvent>? _owner;
        private readonly int _id;
        internal Subscription(AsyncObserver<TEvent> owner, int id) => (_owner, _id) = (owner, id);

        public void Dispose()
        {
            var o = Interlocked.Exchange(ref _owner, null);
            o?.Unsubscribe(_id);
        }
    }

    /// <summary>Create a new fluent builder for <see cref="AsyncObserver{TEvent}"/>.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for configuring error policy and sinks.</summary>
    public sealed class Builder
    {
        private ErrorPolicy _policy = ErrorPolicy.ThrowAggregate;
        private ErrorSink? _sink;

        /// <summary>Send handler exceptions to the provided asynchronous sink (never throws back).</summary>
        /// <param name="sink">The asynchronous error sink.</param>
        /// <returns>The current builder.</returns>
        public Builder OnError(ErrorSink sink)
        {
            _sink = sink;
            return this;
        }

        /// <summary>
        /// Convenience overload for synchronous error sinks.
        /// </summary>
        /// <param name="sink">The synchronous error sink.</param>
        /// <returns>The current builder.</returns>
        public Builder OnError(Observer<TEvent>.ErrorSink sink)
        {
            _sink = (ex, e) =>
            {
                sink(ex, in e);
                return default;
            };
            return this;
        }

        /// <summary>Swallow handler exceptions (after sending to sink if configured).</summary>
        /// <returns>The current builder.</returns>
        public Builder SwallowErrors()
        {
            _policy = ErrorPolicy.Swallow;
            return this;
        }

        /// <summary>Throw the first handler exception immediately.</summary>
        /// <returns>The current builder.</returns>
        public Builder ThrowFirstError()
        {
            _policy = ErrorPolicy.ThrowFirst;
            return this;
        }

        /// <summary>Aggregate all handler exceptions and throw at the end of publish (default).</summary>
        /// <returns>The current builder.</returns>
        public Builder ThrowAggregate()
        {
            _policy = ErrorPolicy.ThrowAggregate;
            return this;
        }

        /// <summary>Build the immutable, thread-safe async observer.</summary>
        /// <returns>A configured <see cref="AsyncObserver{TEvent}"/>.</returns>
        public AsyncObserver<TEvent> Build() => new(_policy, _sink);
    }
}