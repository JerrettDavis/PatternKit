namespace PatternKit.Behavioral.Chain;

/// <summary>
/// An async first-match-wins chain that can <b>produce a value</b>.
/// Each handler receives the input and a continuation, and may either:
/// <list type="bullet">
///   <item><description><b>Produce</b> a value and return it to short-circuit, or</description></item>
///   <item><description><b>Delegate</b> to <c>next</c> so later handlers can attempt to produce.</description></item>
/// </list>
/// </summary>
/// <typeparam name="TIn">The input type threaded through the chain.</typeparam>
/// <typeparam name="TOut">The potential output type produced by handlers.</typeparam>
/// <remarks>
/// <para>
/// Use <see cref="AsyncResultChain{TIn, TOut}"/> for async scenarios where you want ordered rules
/// that compute and return a value (e.g., async routing, async price computation, async parsing).
/// If you only need side effects, prefer <see cref="AsyncActionChain{TCtx}"/>.
/// </para>
/// <para><b>Execution semantics</b></para>
/// <list type="number">
///   <item><description>Handlers are evaluated in registration order.</description></item>
///   <item><description>The first handler that returns a successful result wins and the chain stops.</description></item>
///   <item><description>If no handler produces and no Finally fallback is configured,
///   <see cref="ExecuteAsync"/> returns (false, default).</description></item>
/// </list>
/// <para>
/// <b>Performance:</b> The chain is immutable and thread-safe after build.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// var router = AsyncResultChain<Request, Response>.Create()
///     .When(r => r.Method == "GET" && r.Path == "/health")
///         .Then(async (r, ct) => new Response(200, "OK"))
///     .When(r => r.Path.StartsWith("/users/"))
///         .Then(async (r, ct) => await GetUserAsync(r.Path[7..], ct))
///     .Finally(async (r, ct) => new Response(404, "not found"))
///     .Build();
///
/// var (ok, response) = await router.ExecuteAsync(new Request("GET", "/health"));
/// ]]></code>
/// </example>
public sealed class AsyncResultChain<TIn, TOut>
{
    /// <summary>
    /// Async predicate delegate that decides whether a handler should be invoked.
    /// </summary>
    public delegate ValueTask<bool> Predicate(TIn input, CancellationToken ct);

    /// <summary>
    /// Async handler delegate that may produce a value.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of (success, result). When success is true, result is the produced value.</returns>
    public delegate ValueTask<(bool success, TOut? result)> TryHandler(TIn input, CancellationToken ct);

    /// <summary>
    /// Async producer delegate that always produces a value.
    /// </summary>
    public delegate ValueTask<TOut> Producer(TIn input, CancellationToken ct);

    private readonly TryHandler[] _handlers;
    private readonly TryHandler? _tail;

    private AsyncResultChain(TryHandler[] handlers, TryHandler? tail)
    {
        _handlers = handlers;
        _tail = tail;
    }

    /// <summary>
    /// Executes the chain asynchronously.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of (success, result). When success is true, result is the produced value.</returns>
    public async ValueTask<(bool success, TOut? result)> ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        foreach (var handler in _handlers)
        {
            var (success, result) = await handler(input, ct).ConfigureAwait(false);
            if (success)
                return (true, result);
        }

        if (_tail is not null)
            return await _tail(input, ct).ConfigureAwait(false);

        return (false, default);
    }

    /// <summary>
    /// Fluent builder for <see cref="AsyncResultChain{TIn, TOut}"/>.
    /// </summary>
    public sealed class Builder
    {
        private readonly List<TryHandler> _handlers = new(8);
        private TryHandler? _tail;

        /// <summary>
        /// Appends a handler to the chain.
        /// </summary>
        public Builder Use(TryHandler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        /// <summary>
        /// Starts a conditional block with an async predicate.
        /// </summary>
        public WhenBuilder When(Predicate predicate) => new(this, predicate);

        /// <summary>
        /// Starts a conditional block with a sync predicate.
        /// </summary>
        public WhenBuilder When(Func<TIn, bool> predicate)
        {
            return When(Adapter);
            ValueTask<bool> Adapter(TIn x, CancellationToken _) => new(predicate(x));
        }

        /// <summary>
        /// Sets a terminal fallback that runs when no prior handler produced a result.
        /// </summary>
        public Builder Finally(TryHandler tail)
        {
            _tail = tail;
            return this;
        }

        /// <summary>
        /// Sets a terminal fallback using a producer that always succeeds.
        /// </summary>
        public Builder Finally(Producer producer)
        {
            return Finally(Adapter);
            async ValueTask<(bool, TOut?)> Adapter(TIn x, CancellationToken ct)
            {
                var result = await producer(x, ct).ConfigureAwait(false);
                return (true, result);
            }
        }

        /// <summary>
        /// Sets a terminal fallback using a sync producer.
        /// </summary>
        public Builder Finally(Func<TIn, TOut> producer)
        {
            return Finally(Adapter);
            ValueTask<(bool, TOut?)> Adapter(TIn x, CancellationToken _) => new((true, producer(x)));
        }

        /// <summary>
        /// Builds the immutable async chain.
        /// </summary>
        public AsyncResultChain<TIn, TOut> Build()
            => new(_handlers.ToArray(), _tail);

        /// <summary>
        /// Builder for a conditional branch.
        /// </summary>
        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;

            internal WhenBuilder(Builder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

            /// <summary>
            /// Adds an async producer that returns a value when the predicate is true.
            /// </summary>
            public Builder Then(Producer produce)
            {
                var pred = _pred;
                _owner.Use(Adapter);
                return _owner;

                async ValueTask<(bool, TOut?)> Adapter(TIn x, CancellationToken ct)
                {
                    if (!await pred(x, ct).ConfigureAwait(false))
                        return (false, default);
                    var result = await produce(x, ct).ConfigureAwait(false);
                    return (true, result);
                }
            }

            /// <summary>
            /// Adds a sync producer that returns a value when the predicate is true.
            /// </summary>
            public Builder Then(Func<TIn, TOut> produce)
            {
                var pred = _pred;
                _owner.Use(Adapter);
                return _owner;

                async ValueTask<(bool, TOut?)> Adapter(TIn x, CancellationToken ct)
                {
                    if (!await pred(x, ct).ConfigureAwait(false))
                        return (false, default);
                    return (true, produce(x));
                }
            }
        }
    }

    /// <summary>
    /// Creates a new builder.
    /// </summary>
    public static Builder Create() => new();
}
