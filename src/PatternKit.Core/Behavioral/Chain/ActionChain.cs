using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.Chain;

/// <summary>
/// A tiny, middleware-style pipeline for <typeparamref name="TCtx"/> where each handler decides
/// to call <c>next</c> or short-circuit the chain.
/// </summary>
/// <typeparam name="TCtx">The context type threaded through the chain.</typeparam>
/// <remarks>
/// <para>
/// <see cref="ActionChain{TCtx}"/> is built from a set of ordered handlers. Each handler receives the
/// current context and a <c>next</c> delegate. A handler can either:
/// </para>
/// <list type="bullet">
///   <item><description>Invoke <c>next(in ctx)</c> to continue the chain, or</description></item>
///   <item><description>Return without calling <c>next</c> to short-circuit the chain.</description></item>
/// </list>
/// <para>
/// Registration order is preserved. A terminal <see cref="Builder.Finally(Handler)"/> handler can be provided;
/// it runs only if the chain was not short-circuited earlier (i.e., a previous step called <c>next</c>).
/// After <see cref="Builder.Build"/> the chain is immutable and thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var log = new List&lt;string&gt;();
///
/// var chain = ActionChain&lt;HttpRequest&gt;.Create()
///     // Log request id but continue
///     .When((in r) =&gt; r.Headers.ContainsKey("X-Request-Id"))
///     .ThenContinue(r =&gt; log.Add($"reqid={r.Headers["X-Request-Id"]}"))
///
///     // Deny missing auth for /admin/* and STOP the chain
///     .When((in r) =&gt; r.Path.StartsWith("/admin", StringComparison.Ordinal) &amp;&amp;
///                      !r.Headers.ContainsKey("Authorization"))
///     .ThenStop(r =&gt; log.Add("deny: missing auth"))
///
///     // Tail: logs method/path only if earlier steps continued
///     .Finally((in r, next) =&gt; { log.Add($"{r.Method} {r.Path}"); next(in r); })
///     .Build();
///
/// chain.Execute(new HttpRequest("GET", "/health", new Dictionary&lt;string, string&gt;()));
/// chain.Execute(new HttpRequest("GET", "/admin/metrics", new Dictionary&lt;string, string&gt;()));
/// // log: ["GET /health", "deny: missing auth", "GET /admin/metrics"]
/// </code>
/// </example>
public sealed class ActionChain<TCtx>
{
    /// <summary>
    /// Delegate representing the continuation of the chain.
    /// </summary>
    /// <param name="ctx">The current context.</param>
    public delegate void Next(in TCtx ctx);

    /// <summary>
    /// Delegate representing a chain handler.
    /// </summary>
    /// <param name="ctx">The current context.</param>
    /// <param name="next">
    /// The continuation to invoke to proceed to the next handler. If a handler returns without
    /// calling <paramref name="next"/>, the chain short-circuits.
    /// </param>
    public delegate void Handler(in TCtx ctx, Next next);

    /// <summary>
    /// Delegate representing a predicate over the context.
    /// </summary>
    /// <param name="ctx">The current context.</param>
    /// <returns><see langword="true"/> if the condition is met; otherwise <see langword="false"/>.</returns>
    public delegate bool Predicate(in TCtx ctx);

    private readonly Next _entry;

    private ActionChain(Next entry) => _entry = entry;

    /// <summary>
    /// Executes the composed chain for the provided context.
    /// </summary>
    /// <param name="ctx">The context value passed through the chain.</param>
    /// <remarks>
    /// <para>Execution starts at the first registered handler. Handlers may short-circuit by not calling <c>next</c>.</para>
    /// <para>The terminal continuation is a no-op; calling it in the tail is optional.</para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(in TCtx ctx) => _entry(in ctx);

    /// <summary>
    /// Fluent builder for <see cref="ActionChain{TCtx}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="Use(Handler)"/> to append middleware, <see cref="When(Predicate)"/> to add conditional
    /// blocks, and <see cref="Finally(Handler)"/> to set a terminal tail that runs only if the chain was
    /// not short-circuited. Call <see cref="Build"/> to compose an immutable chain.
    /// </para>
    /// <para><b>Thread safety:</b> The built chain is immutable and thread-safe; the builder is not.</para>
    /// </remarks>
    public sealed class Builder
    {
        private readonly List<Handler> _handlers = new(8);
        private Handler? _tail;

        /// <summary>
        /// Adds a middleware handler to the end of the chain.
        /// </summary>
        /// <param name="handler">The handler to append.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// The <paramref name="handler"/> controls flow by deciding whether to call <c>next</c>.
        /// </remarks>
        public Builder Use(Handler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        /// <summary>
        /// Starts a conditional block. If the predicate is false, the chain automatically continues.
        /// </summary>
        /// <param name="predicate">The condition to evaluate for the current context.</param>
        /// <returns>A <see cref="WhenBuilder"/> that configures the conditional behavior.</returns>
        /// <remarks>
        /// The generated handler ensures that when <paramref name="predicate"/> returns
        /// <see langword="false"/>, <c>next</c> is called automatically.
        /// </remarks>
        public WhenBuilder When(Predicate predicate) => new(this, predicate);

        /// <summary>
        /// Sets the terminal tail handler. It runs only if earlier handlers called <c>next</c> all the way through.
        /// </summary>
        /// <param name="tail">The tail handler to run at the end of the chain.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// If an earlier handler returns without calling <c>next</c>, the tail does not run.
        /// </remarks>
        public Builder Finally(Handler tail)
        {
            _tail = tail;
            return this;
        }

        /// <summary>
        /// Composes all registered handlers (and optional tail) into a single immutable chain.
        /// </summary>
        /// <returns>An executable <see cref="ActionChain{TCtx}"/> instance.</returns>
        /// <remarks>
        /// <para>Handlers are composed in reverse registration order so that execution preserves the original order.</para>
        /// <para>A terminal no-op continuation is used; the tail may call <c>next</c> safely.</para>
        /// </remarks>
        public ActionChain<TCtx> Build()
        {
            // terminal "no-op" continuation
            Next next = static (in _) => { };

            if (_tail is not null)
            {
                var t = _tail;
                var prev = next;
                next = (in c) => t(in c, prev);
            }

            for (var i = _handlers.Count - 1; i >= 0; i--)
            {
                var h = _handlers[i];
                var prev = next;
                next = (in c) => h(in c, prev);
            }

            return new ActionChain<TCtx>(next);
        }

        /// <summary>
        /// Builder for a conditional branch created via <see cref="Builder.When(Predicate)"/>.
        /// </summary>
        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;

            /// <summary>
            /// Initializes a new <see cref="WhenBuilder"/>.
            /// </summary>
            /// <param name="owner">The parent builder.</param>
            /// <param name="pred">The predicate to guard the conditional handler(s).</param>
            internal WhenBuilder(Builder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

            /// <summary>
            /// Adds a conditional handler: when the predicate is <see langword="true"/>, run <paramref name="handler"/>;
            /// otherwise automatically continue the chain.
            /// </summary>
            /// <param name="handler">The handler to execute when the predicate is true.</param>
            /// <returns>The parent builder for chaining.</returns>
            /// <remarks>
            /// The provided <paramref name="handler"/> may itself call or omit <c>next</c> to continue or stop.
            /// </remarks>
            public Builder Do(Handler handler)
            {
                var pred = _pred; // avoid capturing 'this'
                _owner.Use((in c, next) =>
                {
                    if (pred(in c)) handler(in c, next);
                    else next(in c);
                });
                return _owner;
            }

            /// <summary>
            /// Adds a conditional action that executes and <b>stops</b> the chain when the predicate is true.
            /// </summary>
            /// <param name="action">The action to perform before short-circuiting.</param>
            /// <returns>The parent builder for chaining.</returns>
            /// <remarks>
            /// When the predicate is false, the chain continues automatically.
            /// </remarks>
            public Builder ThenStop(Action<TCtx> action)
            {
                var pred = _pred;
                _owner.Use((in c, next) =>
                {
                    if (pred(in c))
                    {
                        action(c);
                        return;
                    }

                    next(in c);
                });
                return _owner;
            }

            /// <summary>
            /// Adds a conditional action that executes and then <b>continues</b> the chain when the predicate is true.
            /// </summary>
            /// <param name="action">The action to perform before continuing.</param>
            /// <returns>The parent builder for chaining.</returns>
            /// <remarks>
            /// When the predicate is false, the chain continues automatically.
            /// </remarks>
            public Builder ThenContinue(Action<TCtx> action)
            {
                var pred = _pred;
                _owner.Use((in c, next) =>
                {
                    if (pred(in c)) action(c);
                    next(in c);
                });
                return _owner;
            }
        }
    }

    /// <summary>
    /// Starts a new <see cref="Builder"/> to configure an <see cref="ActionChain{TCtx}"/>.
    /// </summary>
    /// <returns>A fresh builder instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var chain = ActionChain&lt;MyCtx&gt;.Create()
    ///     .Use((in c, next) =&gt; { /* pre */ next(in c); })
    ///     .Finally((in c, next) =&gt; { /* tail */ next(in c); })
    ///     .Build();
    /// </code>
    /// </example>
    public static Builder Create() => new();
}