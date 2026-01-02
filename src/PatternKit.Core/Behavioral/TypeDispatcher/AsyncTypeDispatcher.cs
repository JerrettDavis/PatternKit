using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.TypeDispatcher;

/// <summary>
/// Type dispatcher (fluent, typed dispatch, async result).
/// Maps runtime types deriving from <typeparamref name="TBase"/> to asynchronous handlers that
/// produce a <typeparamref name="TResult"/> and executes the first matching handler (first-match-wins).
/// </summary>
/// <remarks>
/// <para>
/// This pattern uses runtime type checks to select handlers based on the concrete type of the input.
/// It is similar to the Strategy pattern but dispatches based on type rather than explicit strategy selection.
/// </para>
/// <para>
/// <b>Note:</b> This is NOT the Gang of Four Visitor pattern, which uses double dispatch.
/// For true Visitor pattern with double dispatch, see <see cref="PatternKit.Behavioral.Visitor"/> namespace.
/// </para>
/// <para>
/// Register handlers with <see cref="Builder.On{T}(System.Func{T,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask{TResult}})"/>.
/// Registration order matters; add more specific types before base types. If no handler matches, an optional
/// <see cref="Builder.Default(Handler)"/> is used; otherwise <see cref="DispatchAsync(TBase,System.Threading.CancellationToken)"/>
/// throws and <see cref="TryDispatchAsync(TBase,System.Threading.CancellationToken)"/> returns <c>(false, default)</c>.
/// </para>
/// <para><b>Thread-safety:</b> built instances are immutable and thread-safe; the builder is not thread-safe.</para>
/// </remarks>
/// <typeparam name="TBase">The base type for dispatchable elements.</typeparam>
/// <typeparam name="TResult">The result type of dispatch operations.</typeparam>
public sealed class AsyncTypeDispatcher<TBase, TResult>
{
    /// <summary>Asynchronous predicate to determine if a handler applies.</summary>
    public delegate ValueTask<bool> Predicate(TBase node, CancellationToken ct);

    /// <summary>Asynchronous handler that processes a node and returns a result.</summary>
    public delegate ValueTask<TResult> Handler(TBase node, CancellationToken ct);

    private readonly Predicate[] _predicates;
    private readonly Handler[] _handlers;
    private readonly bool _hasDefault;
    private readonly Handler _default;

    private static Handler DefaultResult => static (_, _) => new ValueTask<TResult>(default(TResult)!);

    private AsyncTypeDispatcher(Predicate[] predicates, Handler[] handlers, bool hasDefault, Handler @default)
        => (_predicates, _handlers, _hasDefault, _default) = (predicates, handlers, hasDefault, @default);

    /// <summary>Dispatches asynchronously to the first matching handler and returns the result.</summary>
    /// <param name="node">The node to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the matched handler, or the default if configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler matches and no default is configured.</exception>
    public async ValueTask<TResult> DispatchAsync(TBase node, CancellationToken ct = default)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (await predicates[i](node, ct).ConfigureAwait(false))
                return await _handlers[i](node, ct).ConfigureAwait(false);

        if (_hasDefault)
            return await _default(node, ct).ConfigureAwait(false);

        return Throw.NoStrategyMatched<TResult>();
    }

    /// <summary>Attempts to dispatch asynchronously; never throws for no-match. Returns a tuple indicating success and result.</summary>
    public async ValueTask<(bool ok, TResult result)> TryDispatchAsync(TBase node, CancellationToken ct = default)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (await predicates[i](node, ct).ConfigureAwait(false))
            {
                var value = await _handlers[i](node, ct).ConfigureAwait(false);
                return (true, value);
            }

        if (_hasDefault)
        {
            var value = await _default(node, ct).ConfigureAwait(false);
            return (true, value);
        }

        return (false, default!);
    }

    /// <summary>Create a new fluent builder for a <see cref="AsyncTypeDispatcher{TBase, TResult}"/>.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder to register type-specific async handlers and an optional default.</summary>
    public sealed class Builder
    {
        private readonly BranchBuilder<Predicate, Handler> _core = BranchBuilder<Predicate, Handler>.Create();

        /// <summary>Registers an async handler for nodes of type <typeparamref name="T"/>.</summary>
        public Builder On<T>(Func<T, CancellationToken, ValueTask<TResult>> handler) where T : TBase
        {
            _core.Add(Is<T>, Wrap(handler));
            return this;
        }

        /// <summary>Registers a sync handler for nodes of type <typeparamref name="T"/>.</summary>
        public Builder On<T>(Func<T, TResult> handler) where T : TBase
            => On<T>((x, _) => new ValueTask<TResult>(handler(x)));

        /// <summary>Registers a constant result for nodes of type <typeparamref name="T"/>.</summary>
        public Builder On<T>(TResult constant) where T : TBase
            => On<T>((_, _) => new ValueTask<TResult>(constant));

        /// <summary>Sets an async default handler for nodes with no matching registration.</summary>
        public Builder Default(Handler handler)
        {
            _core.Default(handler);
            return this;
        }

        /// <summary>Sets a sync default handler for nodes with no matching registration.</summary>
        public Builder Default(Func<TBase, TResult> handler)
            => Default((x, _) => new ValueTask<TResult>(handler(x)));

        /// <summary>Builds the immutable, thread-safe type dispatcher.</summary>
        public AsyncTypeDispatcher<TBase, TResult> Build()
            => _core.Build(
                fallbackDefault: DefaultResult,
                projector: static (predicates, handlers, hasDefault, @default)
                    => new AsyncTypeDispatcher<TBase, TResult>(predicates, handlers, hasDefault, @default));

        private static ValueTask<bool> Is<T>(TBase node, CancellationToken _) where T : TBase
            => new(node is T);

        private static Handler Wrap<T>(Func<T, CancellationToken, ValueTask<TResult>> typed) where T : TBase
            => (node, ct) => typed((T)node!, ct);
    }
}
