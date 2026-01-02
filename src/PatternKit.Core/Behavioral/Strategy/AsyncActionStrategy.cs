using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Strategy;

/// <summary>
/// Composable, asynchronous action strategy that selects the first matching branch
/// (predicate + handler) and executes its handler for side effects.
/// </summary>
/// <typeparam name="TIn">The input type supplied to predicates and handlers.</typeparam>
/// <remarks>
/// <para>
/// This strategy evaluates predicates in the order they were added. The first
/// predicate that returns <see langword="true"/> determines the chosen handler.
/// If no predicates match, an optional <see cref="Builder.Default(Handler)"/> handler
/// is invoked. Without a default, <see cref="ExecuteAsync(TIn, CancellationToken)"/>
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
/// var logs = new List&lt;string&gt;();
/// var strat = AsyncActionStrategy&lt;int&gt;.Create()
///     .When((n, ct) => new ValueTask&lt;bool&gt;(n &lt; 0))
///         .Then(async (n, ct) => { await Task.Delay(10, ct); logs.Add("negative"); })
///     .When((n, ct) => new ValueTask&lt;bool&gt;(n == 0))
///         .Then((n, ct) => { logs.Add("zero"); return default; })
///     .Default((n, ct) => { logs.Add("positive"); return default; })
///     .Build();
///
/// await strat.ExecuteAsync(5, CancellationToken.None); // logs "positive"
/// </code>
/// </example>
/// <seealso cref="Builder"/>
public sealed class AsyncActionStrategy<TIn>
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
    /// Asynchronous handler delegate that performs side effects for a matching branch.
    /// </summary>
    /// <param name="input">The input value to handle.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the handler finishes.</returns>
    public delegate ValueTask Handler(TIn input, CancellationToken ct);

    private readonly Predicate[] _predicates;
    private readonly Handler[] _handlers;
    private readonly bool _hasDefault;
    private readonly Handler _default;

    // Internal fallback used by the builder when a default is not provided.
    private static Handler DefaultHandler => (_, _) => default;

    private AsyncActionStrategy(Predicate[] preds, Handler[] handlers, bool hasDefault, Handler def) =>
        (_predicates, _handlers, _hasDefault, _default) = (preds, handlers, hasDefault, def);

    /// <summary>
    /// Executes the strategy by evaluating predicates in order and invoking the first matching handler.
    /// </summary>
    /// <param name="input">The input value passed to predicates and handlers.</param>
    /// <param name="ct">An optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the chosen handler finishes.</returns>
    /// <remarks>
    /// If no predicates match and a default handler was configured, the default handler is invoked.
    /// Otherwise the method throws to indicate that no branch matched.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no predicate matches and no default handler is configured.
    /// </exception>
    public async ValueTask ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        var preds = _predicates;
        for (var i = 0; i < preds.Length; i++)
        {
            if (await preds[i](input, ct).ConfigureAwait(false))
            {
                await _handlers[i](input, ct).ConfigureAwait(false);
                return;
            }
        }

        if (_hasDefault)
        {
            await _default(input, ct).ConfigureAwait(false);
            return;
        }

        Throw.NoStrategyMatched();
    }

    /// <summary>
    /// Tries to execute the strategy. Returns <see langword="true"/> if a handler was executed.
    /// </summary>
    /// <param name="input">The input value passed to predicates and handlers.</param>
    /// <param name="ct">An optional cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if a predicate matched and its handler was executed;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public async ValueTask<bool> TryExecuteAsync(TIn input, CancellationToken ct = default)
    {
        var preds = _predicates;
        for (var i = 0; i < preds.Length; i++)
        {
            if (await preds[i](input, ct).ConfigureAwait(false))
            {
                await _handlers[i](input, ct).ConfigureAwait(false);
                return true;
            }
        }

        if (_hasDefault)
        {
            await _default(input, ct).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Fluent builder for composing <see cref="AsyncActionStrategy{TIn}"/> branches.
    /// </summary>
    /// <remarks>
    /// Use <see cref="When(Predicate)"/> to add predicate/handler branches and
    /// <see cref="Default(Handler)"/> to set an optional fallback handler.
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
        public WhenBuilder When(Predicate pred) => new(this, pred);

        /// <summary>
        /// Sets the default (fallback) handler used when no predicates match.
        /// </summary>
        /// <param name="handler">The asynchronous default handler.</param>
        /// <returns>The current <see cref="Builder"/> for chaining.</returns>
        public Builder Default(Handler handler)
        {
            _core.Default(handler);
            return this;
        }

        /// <summary>
        /// Finalizes the configuration and creates an immutable <see cref="AsyncActionStrategy{TIn}"/>.
        /// </summary>
        /// <returns>An immutable strategy instance.</returns>
        public AsyncActionStrategy<TIn> Build() =>
            _core.Build(
                fallbackDefault: DefaultHandler,
                projector: static (p, h, hasDef, def) => new AsyncActionStrategy<TIn>(p, h, hasDef, def));

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
            public Builder Then(Handler handler)
            {
                _owner._core.Add(_pred, handler);
                return _owner;
            }

            /// <summary>
            /// Assigns a synchronous handler to execute when the associated predicate evaluates to <see langword="true"/>.
            /// </summary>
            /// <param name="syncHandler">A synchronous handler; wrapped as async.</param>
            /// <returns>The parent <see cref="Builder"/> for further configuration.</returns>
            public Builder Then(Action<TIn> syncHandler)
            {
                return Then(Adapter);
                ValueTask Adapter(TIn x, CancellationToken _)
                {
                    syncHandler(x);
                    return default;
                }
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
            ValueTask<bool> Adapter(TIn x, CancellationToken _) => new(syncPred(x));
        }

        /// <summary>
        /// Adds a branch using a synchronous predicate that accepts a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="syncPredWithCt">Synchronous predicate receiving a token; wrapped as async.</param>
        /// <returns>A <see cref="WhenBuilder"/> to specify the handler.</returns>
        public WhenBuilder When(Func<TIn, CancellationToken, bool> syncPredWithCt)
        {
            return When(Adapter);
            ValueTask<bool> Adapter(TIn x, CancellationToken ct) => new(syncPredWithCt(x, ct));
        }

        /// <summary>
        /// Sets the default (fallback) handler using a synchronous delegate.
        /// </summary>
        /// <param name="syncHandler">Synchronous default handler; wrapped as async.</param>
        /// <returns>The current <see cref="Builder"/> for chaining.</returns>
        public Builder Default(Action<TIn> syncHandler)
        {
            return Default(Adapter);
            ValueTask Adapter(TIn x, CancellationToken _)
            {
                syncHandler(x);
                return default;
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="Builder"/> for configuring an <see cref="AsyncActionStrategy{TIn}"/>.
    /// </summary>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    public static Builder Create() => new();
}
