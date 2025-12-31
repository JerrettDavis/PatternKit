using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.TypeDispatcher;

/// <summary>
/// Type dispatcher (fluent, typed dispatch, result).
/// Maps runtime types deriving from <typeparamref name="TBase"/> to result-producing handlers
/// and executes the first matching handler (first-match-wins).
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
/// Use <see cref="Builder.On{T}(System.Func{T,TResult})"/> to register handlers for concrete types
/// (<c>where T : TBase</c>). Registration order matters: register more specific types before base types.
/// If no handler matches, an optional <see cref="Builder.Default(Handler)"/> is used; otherwise
/// <see cref="Dispatch(in TBase)"/> throws and <see cref="TryDispatch(in TBase, out TResult)"/> returns <see langword="false"/>.
/// </para>
/// <para>
/// <b>Thread-safety:</b> instances built via <see cref="Builder.Build"/> are immutable and thread-safe for
/// concurrent use, assuming supplied handlers are themselves thread-safe. The builder is not thread-safe.
/// </para>
/// <para>
/// <b>Performance:</b> dispatch is performed by evaluating registered predicates in order. For large numbers of
/// registrations or hot paths, consider grouping by type or composing multiple dispatchers for locality.
/// </para>
/// </remarks>
/// <typeparam name="TBase">The base type for dispatchable elements.</typeparam>
/// <typeparam name="TResult">The return type of dispatch operations.</typeparam>
public sealed class TypeDispatcher<TBase, TResult>
{
    /// <summary>Predicate to determine if a handler applies to a node.</summary>
    /// <param name="node">The node value.</param>
    /// <returns><see langword="true"/> if the handler applies; otherwise <see langword="false"/>.</returns>
    public delegate bool Predicate(in TBase node);

    /// <summary>Handler that processes a node and returns a result.</summary>
    /// <param name="node">The node value.</param>
    /// <returns>The computed result.</returns>
    public delegate TResult Handler(in TBase node);

    private readonly Predicate[] _predicates;
    private readonly Handler[] _handlers;
    private readonly bool _hasDefault;
    private readonly Handler _default;

    private static Handler DefaultResult => static (in _) => default!;

    private TypeDispatcher(Predicate[] predicates, Handler[] handlers, bool hasDefault, Handler @default)
        => (_predicates, _handlers, _hasDefault, _default) = (predicates, handlers, hasDefault, @default);

    /// <summary>Dispatches to the first matching handler and returns the result, or uses the default.</summary>
    /// <param name="node">The node to dispatch.</param>
    /// <returns>The result of the matched handler, or the default if configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler matches and no default is configured.</exception>
    public TResult Dispatch(in TBase node)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in node))
                return _handlers[i](in node);

        return _hasDefault
            ? _default(in node)
            : Throw.NoStrategyMatched<TResult>();
    }

    /// <summary>Attempts to dispatch to a handler; never throws for no-match.</summary>
    /// <param name="node">The node to dispatch.</param>
    /// <param name="result">The result if a handler or default executed.</param>
    /// <returns><see langword="true"/> if a handler (or default) executed; otherwise <see langword="false"/>.</returns>
    public bool TryDispatch(in TBase node, out TResult result)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in node))
            {
                result = _handlers[i](in node);
                return true;
            }

        if (_hasDefault)
        {
            result = _default(in node);
            return true;
        }

        result = default!;
        return false;
    }

    /// <summary>Creates a new fluent builder for a <see cref="TypeDispatcher{TBase, TResult}"/>.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder to register type-specific handlers and an optional default.</summary>
    public sealed class Builder
    {
        private readonly BranchBuilder<Predicate, Handler> _core = BranchBuilder<Predicate, Handler>.Create();

        /// <summary>Registers a handler for nodes of type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">A concrete type assignable to <typeparamref name="TBase"/>.</typeparam>
        /// <param name="handler">The handler invoked when the runtime type is <typeparamref name="T"/>.</param>
        public Builder On<T>(Func<T, TResult> handler) where T : TBase
        {
            _core.Add(Is<T>, Wrap(handler));
            return this;
        }

        /// <summary>Registers a constant result for nodes of type <typeparamref name="T"/>.</summary>
        public Builder On<T>(TResult constant) where T : TBase
            => On<T>(_ => constant);

        /// <summary>Sets a default handler for nodes with no matching registration.</summary>
        public Builder Default(Handler handler)
        {
            _core.Default(handler);
            return this;
        }

        /// <summary>Sets a default handler using a synchronous delegate without <c>in</c> parameter syntax.</summary>
        public Builder Default(Func<TBase, TResult> handler)
            => Default((in x) => handler(x));

        /// <summary>Builds the immutable, thread-safe type dispatcher.</summary>
        public TypeDispatcher<TBase, TResult> Build()
            => _core.Build(
                fallbackDefault: DefaultResult,
                projector: static (predicates, handlers, hasDefault, @default)
                    => new TypeDispatcher<TBase, TResult>(predicates, handlers, hasDefault, @default));

        private static bool Is<T>(in TBase node) where T : TBase => node is T;

        private static Handler Wrap<T>(Func<T, TResult> typed) where T : TBase
            => (in node) => typed((T)node!);
    }
}
