using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.Observer;

/// <summary>
/// Observer (typed, fluent, thread-safe)
/// Provides a subscription mechanism to notify multiple observers about events of type <typeparamref name="TEvent"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is designed for high read/concurrent publish scenarios using a copy-on-write subscription array.
/// Subscriptions and unsubscriptions are atomic and lock-free; publishing snapshots the current array and iterates it
/// without locks. The builder configures error handling behavior.
/// </para>
/// <para>After <see cref="Create"/> and <see cref="Builder.Build"/>, the observer instance is immutable and thread-safe.</para>
/// </remarks>
/// <typeparam name="TEvent">The event payload type to broadcast.</typeparam>
public sealed class Observer<TEvent>
{
    /// <summary>Predicate to filter whether a handler should receive the event.</summary>
    /// <param name="evt">The event.</param>
    /// <returns><see langword="true"/> to invoke the handler; otherwise <see langword="false"/>.</returns>
    public delegate bool Predicate(in TEvent evt);

    /// <summary>Handler invoked when a published event passes an optional predicate.</summary>
    /// <param name="evt">The event payload.</param>
    public delegate void Handler(in TEvent evt);

    /// <summary>Error sink invoked when a handler throws; never throws back into the publisher.</summary>
    /// <param name="ex">The exception from a handler.</param>
    /// <param name="evt">The event being processed.</param>
    public delegate void ErrorSink(Exception ex, in TEvent evt);

    private enum ErrorPolicy { Swallow, ThrowFirst, ThrowAggregate }

    private readonly ErrorPolicy _errorPolicy;
    private readonly ErrorSink? _errorSink;

    private Entry[] _entries = []; // copy-on-write storage
    private int _nextId; // subscription id counter

    private struct Entry
    {
        public int Id;
        public Predicate? Pred;
        public Handler Callback;
    }

    private Observer(ErrorPolicy errorPolicy, ErrorSink? errorSink)
        => (_errorPolicy, _errorSink) = (errorPolicy, errorSink);

    /// <summary>Current number of active subscriptions (approximate during concurrent updates).</summary>
    public int SubscriberCount => Volatile.Read(ref _entries).Length;

    /// <summary>
    /// Publish an event to all matching subscribers. Exceptions are handled according to the configured policy.
    /// </summary>
    /// <param name="evt">The event value.</param>
    /// <exception cref="AggregateException">When policy is ThrowAggregate and one or more handlers threw.</exception>
    /// <exception cref="Exception">When policy is ThrowFirst and a handler threw; the first exception is rethrown.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(in TEvent evt)
    {
        var snapshot = Volatile.Read(ref _entries);
        List<Exception>? errors = null;

        foreach (var t in snapshot)
        {
            ref readonly var e = ref t;
            var run = e.Pred is null || e.Pred(in evt);
            if (!run) continue;

            try { e.Callback(in evt); }
            catch (Exception ex)
            {
                // Best-effort sink; never throw from sink.
                var sink = _errorSink;
                if (sink is not null)
                {
                    try { sink(ex, in evt); } catch { /* swallow */ }
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

    /// <summary>
    /// Subscribe a handler to receive all events.
    /// </summary>
    /// <param name="handler">The handler to invoke.</param>
    /// <returns>An <see cref="IDisposable"/> that removes the subscription when disposed.</returns>
    public IDisposable Subscribe(Handler handler) => Subscribe(null, handler);

    /// <summary>
    /// Subscribe a handler with an optional predicate filter.
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

    private void Unsubscribe(int id)
    {
        while (true)
        {
            var curr = Volatile.Read(ref _entries);
            var idx = -1;
            for (var i = 0; i < curr.Length; i++)
            {
                if (curr[i].Id != id) continue;
                
                idx = i; break;
            }

            if (idx < 0) return; // already removed

            var next = new Entry[curr.Length - 1];
            if (idx > 0) Array.Copy(curr, 0, next, 0, idx);
            if (idx < curr.Length - 1) Array.Copy(curr, idx + 1, next, idx, curr.Length - idx - 1);

            if (Interlocked.CompareExchange(ref _entries, next, curr) == curr)
                return;
        }
    }

    private sealed class Subscription : IDisposable
    {
        private Observer<TEvent>? _owner;
        private readonly int _id;

        internal Subscription(Observer<TEvent> owner, int id)
            => (_owner, _id) = (owner, id);

        public void Dispose()
        {
            var o = Interlocked.Exchange(ref _owner, null);
            o?.Unsubscribe(_id);
        }
    }

    /// <summary>Create a new fluent builder for <see cref="Observer{TEvent}"/>.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for configuring error policy and sinks.</summary>
    public sealed class Builder
    {
        private ErrorPolicy _policy = ErrorPolicy.ThrowAggregate;
        private ErrorSink? _sink;

        /// <summary>Send handler exceptions to the provided sink (never throws back).</summary>
        public Builder OnError(ErrorSink sink)
        {
            _sink = sink;
            return this;
        }

        /// <summary>Swallow handler exceptions (after sending to sink if configured).</summary>
        public Builder SwallowErrors()
        {
            _policy = ErrorPolicy.Swallow;
            return this;
        }

        /// <summary>Throw the first handler exception immediately.</summary>
        public Builder ThrowFirstError()
        {
            _policy = ErrorPolicy.ThrowFirst;
            return this;
        }

        /// <summary>Aggregate all handler exceptions and throw at the end of publish (default).</summary>
        public Builder ThrowAggregate()
        {
            _policy = ErrorPolicy.ThrowAggregate;
            return this;
        }

        /// <summary>Build the immutable, thread-safe observer.</summary>
        public Observer<TEvent> Build() => new(_policy, _sink);
    }
}
