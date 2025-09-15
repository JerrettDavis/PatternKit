using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Strategy;

/// <summary>
/// Composable, asynchronous strategy that selects the first matching branch
/// (predicate + handler) and executes its handler.
/// </summary>
/// <typeparam name="TIn">The input type supplied to predicates and handlers.</typeparam>
/// <typeparam name="TOut">The result type returned by handlers.</typeparam>
/// <remarks>
/// <para>
/// This strategy evaluates predicates in the order they were added. The first
/// predicate that returns <see langword="true"/> determines the chosen handler.
/// If no predicates match, an optional <see cref="Builder.Default(Handler)"/> handler
/// is invoked. Without a default, <see cref="ExecuteAsync(TIn, System.Threading.CancellationToken)"/>
/// throws to signal that no branch matched.
/// </para>
/// <para>
/// Instances built via <see cref="Builder.Build"/> are immutable and thread-safe
/// for concurrent execution, assuming supplied predicates/handlers are thread-safe.
/// </para>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// var strat = AsyncStrategy&lt;int, string&gt;.Create()
///     .When((n, ct) => new ValueTask&lt;bool&gt;(n &lt; 0))
///         .Then((n, ct) => new ValueTask&lt;string&gt;("negative"))
///     .When((n, ct) => new ValueTask&lt;bool&gt;(n == 0))
///         .Then((n, ct) => new ValueTask&lt;string&gt;("zero"))
///     .Default((n, ct) => new ValueTask&lt;string&gt;("positive"))
///     .Build();
///
/// var result = await strat.ExecuteAsync(5, CancellationToken.None); // "positive"
/// </code>
/// </example>
/// <seealso cref="Builder"/>
public sealed class AsyncStrategy<TIn, TOut>
{
    /// <summary>
    /// Asynchronous predicate delegate that decides whether a branch can handle the input.
    /// </summary>
    /// <param name="input">The input value to evaluate.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> producing <see langword="true"/> if the branch
    /// should handle the input; otherwise <see langword="false"/>.
    /// </returns>
    public delegate ValueTask<bool> Predicate(TIn input, CancellationToken ct);

    /// <summary>
    /// Asynchronous handler delegate that produces the result for a matching branch.
    /// </summary>
    /// <param name="input">The input value to handle.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> producing the result for the branch.
    /// </returns>
    public delegate ValueTask<TOut> Handler(TIn input, CancellationToken ct);

    private readonly Predicate[] _predicates;
    private readonly Handler[] _handlers;
    private readonly bool _hasDefault;
    private readonly Handler _default;

    // Internal fallback used by the builder when a default is not provided.
    private static Handler DefaultResult =>
        (_, _) => new ValueTask<TOut>(default(TOut)!);

    private AsyncStrategy(Predicate[] preds, Handler[] handlers, bool hasDefault, Handler def) =>
        (_predicates, _handlers, _hasDefault, _default) = (preds, handlers, hasDefault, def);

    /// <summary>
    /// Executes the strategy by evaluating predicates in order and invoking the first matching handler.
    /// </summary>
    /// <param name="input">The input value passed to predicates and handlers.</param>
    /// <param name="ct">An optional cancellation token.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that completes with the result of the chosen handler.
    /// </returns>
    /// <remarks>
    /// If no predicates match and a default handler was configured, the default handler is invoked.
    /// Otherwise the method throws to indicate that no branch matched.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when no predicate matches and no default handler is configured.
    /// </exception>
    public async ValueTask<TOut> ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        var preds = _predicates;
        for (var i = 0; i < preds.Length; i++)
            if (await preds[i](input, ct).ConfigureAwait(false))
                return await _handlers[i](input, ct).ConfigureAwait(false);

        if (_hasDefault)
            return await _default(input, ct).ConfigureAwait(false);

        return Throw.NoStrategyMatched<TOut>();
    }

    /// <summary>
    /// Fluent builder for composing <see cref="AsyncStrategy{TIn, TOut}"/> branches.
    /// </summary>
    /// <remarks>
    /// Use <see cref="When(Predicate)"/> to add predicate/handler branches and
    /// <see cref="Default(Handler)"/> to set an optional fallback handler. Supports
    /// synchronous adapters for convenience—see <see cref="When(System.Func{TIn, bool})"/>,
    /// <see cref="When(System.Func{TIn, System.Threading.CancellationToken, bool})"/>,
    /// and <see cref="Default(System.Func{TIn, TOut})"/>.
    /// </remarks>
    public sealed class Builder
    {
        private readonly BranchBuilder<Predicate, Handler> _core = BranchBuilder<Predicate, Handler>.Create();

        /// <summary>
        /// Adds a new branch that will be considered during execution.
        /// </summary>
        /// <param name="pred">An asynchronous predicate for the branch.</param>
        /// <returns>
        /// A <see cref="WhenBuilder"/> that allows specifying the corresponding handler
        /// via <see cref="WhenBuilder.Then(Handler)"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pred"/> is <see langword="null"/>.</exception>
        public WhenBuilder When(Predicate pred) => new(this, pred);

        /// <summary>
        /// Sets the default (fallback) handler used when no predicates match.
        /// </summary>
        /// <param name="handler">The asynchronous default handler.</param>
        /// <returns>The current <see cref="Builder"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
        public Builder Default(Handler handler)
        {
            _core.Default(handler);
            return this;
        }

        /// <summary>
        /// Finalizes the configuration and creates an immutable <see cref="AsyncStrategy{TIn, TOut}"/>.
        /// </summary>
        /// <returns>An immutable strategy instance.</returns>
        public AsyncStrategy<TIn, TOut> Build() =>
            _core.Build(
                fallbackDefault: DefaultResult,
                projector: static (p, h, hasDef, def) => new AsyncStrategy<TIn, TOut>(p, h, hasDef, def));

        /// <summary>
        /// Intermediate builder returned by <see cref="When(Predicate)"/> allowing a corresponding handler to be set.
        /// </summary>
        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;

            internal WhenBuilder(Builder owner, Predicate pred)
            {
                _owner = owner;
                _pred = pred;
            }

            /// <summary>
            /// Assigns the handler to execute when the associated predicate evaluates to <see langword="true"/>.
            /// </summary>
            /// <param name="handler">The asynchronous handler for this branch.</param>
            /// <returns>The parent <see cref="Builder"/> for further configuration.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
            public Builder Then(Handler handler)
            {
                _owner._core.Add(_pred, handler);
                return _owner;
            }
        }

        // -------- Synchronous adapters --------

        /// <summary>
        /// Adds a branch using a synchronous predicate (no <see cref="CancellationToken"/>).
        /// </summary>
        /// <param name="syncPred">Synchronous predicate; wrapped as an async predicate.</param>
        /// <returns>A <see cref="WhenBuilder"/> to specify the handler.</returns>
        public WhenBuilder When(Func<TIn, bool> syncPred)
        {
            return When(Adapter);
            ValueTask<bool> Adapter(TIn x, CancellationToken _) => new ValueTask<bool>(syncPred(x));
        }

        /// <summary>
        /// Adds a branch using a synchronous predicate that accepts a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="syncPredWithCt">Synchronous predicate receiving a token; wrapped as async.</param>
        /// <returns>A <see cref="WhenBuilder"/> to specify the handler.</returns>
        public WhenBuilder When(Func<TIn, CancellationToken, bool> syncPredWithCt)
        {
            return When(Adapter);
            ValueTask<bool> Adapter(TIn x, CancellationToken ct) => new ValueTask<bool>(syncPredWithCt(x, ct));
        }

        /// <summary>
        /// Sets the default (fallback) handler using a synchronous delegate.
        /// </summary>
        /// <param name="syncHandler">Synchronous default handler; wrapped as async.</param>
        /// <returns>The current <see cref="Builder"/> for chaining.</returns>
        public Builder Default(Func<TIn, TOut> syncHandler)
        {
            return Default(Adapter);
            ValueTask<TOut> Adapter(TIn x, CancellationToken _) => new ValueTask<TOut>(syncHandler(x));
        }
    }

    /// <summary>
    /// Creates a new <see cref="Builder"/> for configuring an <see cref="AsyncStrategy{TIn, TOut}"/>.
    /// </summary>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    public static Builder Create() => new();
}