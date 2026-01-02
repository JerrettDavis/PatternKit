namespace PatternKit.Behavioral.Chain;

/// <summary>
/// An async middleware-style pipeline for <typeparamref name="TCtx"/> where each handler decides
/// to call <c>next</c> or short-circuit the chain.
/// </summary>
/// <typeparam name="TCtx">The context type threaded through the chain.</typeparam>
/// <remarks>
/// <para>
/// <see cref="AsyncActionChain{TCtx}"/> is built from a set of ordered async handlers. Each handler
/// receives the current context, a cancellation token, and a <c>next</c> delegate. A handler can either:
/// </para>
/// <list type="bullet">
///   <item><description>Invoke <c>next(ctx, ct)</c> to continue the chain, or</description></item>
///   <item><description>Return without calling <c>next</c> to short-circuit the chain.</description></item>
/// </list>
/// <para>
/// Registration order is preserved. A terminal Finally handler can be provided;
/// it runs only if the chain was not short-circuited earlier.
/// After <see cref="Builder.Build"/> the chain is immutable and thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp"><![CDATA[
/// var logs = new List<string>();
///
/// var chain = AsyncActionChain<HttpRequest>.Create()
///     // Log request id but continue
///     .When(r => r.Headers.ContainsKey("X-Request-Id"))
///     .ThenContinue(async (r, ct) => { logs.Add($"reqid={r.Headers["X-Request-Id"]}"); })
///
///     // Deny missing auth for /admin/* and STOP the chain
///     .When(r => r.Path.StartsWith("/admin") && !r.Headers.ContainsKey("Authorization"))
///     .ThenStop(async (r, ct) => { logs.Add("deny: missing auth"); })
///
///     // Tail: logs method/path only if earlier steps continued
///     .Finally(async (r, ct) => { logs.Add($"{r.Method} {r.Path}"); })
///     .Build();
///
/// await chain.ExecuteAsync(new HttpRequest("GET", "/health", new()));
/// ]]></code>
/// </example>
public sealed class AsyncActionChain<TCtx>
{
    /// <summary>
    /// Async continuation delegate.
    /// </summary>
    public delegate ValueTask Next(TCtx ctx, CancellationToken ct);

    /// <summary>
    /// Async handler delegate that receives context, cancellation token, and continuation.
    /// </summary>
    public delegate ValueTask Handler(TCtx ctx, CancellationToken ct, Next next);

    /// <summary>
    /// Async predicate delegate.
    /// </summary>
    public delegate ValueTask<bool> Predicate(TCtx ctx, CancellationToken ct);

    /// <summary>
    /// Async action delegate (no continuation).
    /// </summary>
    public delegate ValueTask Action(TCtx ctx, CancellationToken ct);

    private readonly Handler[] _handlers;
    private readonly Action? _tail;

    private AsyncActionChain(Handler[] handlers, Action? tail)
    {
        _handlers = handlers;
        _tail = tail;
    }

    /// <summary>
    /// Executes the chain asynchronously.
    /// </summary>
    public async ValueTask ExecuteAsync(TCtx ctx, CancellationToken ct = default)
    {
        var index = 0;

        ValueTask ExecuteNext(TCtx c, CancellationToken token)
        {
            if (index < _handlers.Length)
            {
                var handler = _handlers[index++];
                return handler(c, token, ExecuteNext);
            }

            if (_tail is not null)
                return _tail(c, token);

            return default;
        }

        await ExecuteNext(ctx, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Fluent builder for <see cref="AsyncActionChain{TCtx}"/>.
    /// </summary>
    public sealed class Builder
    {
        private readonly List<Handler> _handlers = new(8);
        private Action? _tail;

        /// <summary>
        /// Appends a raw handler to the chain.
        /// </summary>
        public Builder Use(Handler handler)
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
        public WhenBuilder When(Func<TCtx, bool> predicate)
        {
            return When(Adapter);
            ValueTask<bool> Adapter(TCtx x, CancellationToken _) => new(predicate(x));
        }

        /// <summary>
        /// Sets a terminal handler that runs when the chain reaches the tail.
        /// </summary>
        public Builder Finally(Action tail)
        {
            _tail = tail;
            return this;
        }

        /// <summary>
        /// Sets a terminal handler with a sync action.
        /// </summary>
        public Builder Finally(System.Action<TCtx> tail)
        {
            return Finally(Adapter);
            ValueTask Adapter(TCtx x, CancellationToken _)
            {
                tail(x);
                return default;
            }
        }

        /// <summary>
        /// Builds the immutable async chain.
        /// </summary>
        public AsyncActionChain<TCtx> Build()
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
            /// Executes the action and then continues to next handler.
            /// </summary>
            public Builder ThenContinue(Action action)
            {
                var pred = _pred;
                _owner.Use(Adapter);
                return _owner;

                async ValueTask Adapter(TCtx ctx, CancellationToken ct, Next next)
                {
                    if (await pred(ctx, ct).ConfigureAwait(false))
                        await action(ctx, ct).ConfigureAwait(false);
                    await next(ctx, ct).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Executes the action and then continues to next handler (sync action).
            /// </summary>
            public Builder ThenContinue(System.Action<TCtx> action)
            {
                return ThenContinue(Adapter);
                ValueTask Adapter(TCtx x, CancellationToken _)
                {
                    action(x);
                    return default;
                }
            }

            /// <summary>
            /// Executes the action and short-circuits the chain (stops).
            /// </summary>
            public Builder ThenStop(Action action)
            {
                var pred = _pred;
                _owner.Use(Adapter);
                return _owner;

                async ValueTask Adapter(TCtx ctx, CancellationToken ct, Next next)
                {
                    if (await pred(ctx, ct).ConfigureAwait(false))
                    {
                        await action(ctx, ct).ConfigureAwait(false);
                        return; // short-circuit
                    }
                    await next(ctx, ct).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Executes the action and short-circuits the chain (sync action).
            /// </summary>
            public Builder ThenStop(System.Action<TCtx> action)
            {
                return ThenStop(Adapter);
                ValueTask Adapter(TCtx x, CancellationToken _)
                {
                    action(x);
                    return default;
                }
            }
        }
    }

    /// <summary>
    /// Creates a new builder.
    /// </summary>
    public static Builder Create() => new();
}
