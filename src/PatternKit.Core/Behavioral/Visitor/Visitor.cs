using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Type-based dispatcher (fluent, typed dispatch, result).
/// Maps runtime types deriving from <typeparamref name="TBase"/> to result-producing handlers
/// and executes the first matching handler (first-match-wins).
/// </summary>
/// <remarks>
/// <para>
/// <b>DEPRECATION NOTICE:</b> This class has been renamed to <see cref="TypeDispatcher.TypeDispatcher{TBase, TResult}"/>
/// to better reflect its actual behavior. This class uses runtime type checking (Strategy-like pattern),
/// NOT the Gang of Four Visitor pattern with double dispatch.
/// </para>
/// <para>
/// Use <see cref="Builder.On{T}(System.Func{T,TResult})"/> to register handlers for concrete types
/// (<c>where T : TBase</c>). Registration order matters: register more specific types before base types.
/// If no handler matches, an optional <see cref="Builder.Default(Handler)"/> is used; otherwise
/// <see cref="Visit(in TBase)"/> throws and <see cref="TryVisit(in TBase, out TResult)"/> returns <see langword="false"/>.
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
/// <typeparam name="TBase">The base type for visitable elements.</typeparam>
/// <typeparam name="TResult">The return type of visit operations.</typeparam>
[Obsolete("Use TypeDispatcher<TBase, TResult> instead. This class has been renamed to better reflect its behavior (type-based dispatch, not GoF Visitor pattern).")]
public sealed class Visitor<TBase, TResult>
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

    private Visitor(Predicate[] predicates, Handler[] handlers, bool hasDefault, Handler @default)
        => (_predicates, _handlers, _hasDefault, _default) = (predicates, handlers, hasDefault, @default);

    /// <summary>Visits a node and returns the result of the first matching handler or the default.</summary>
    /// <param name="node">The node to visit.</param>
    /// <returns>The result of the matched handler, or the default if configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler matches and no default is configured.</exception>
    public TResult Visit(in TBase node)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in node))
                return _handlers[i](in node);

        return _hasDefault
            ? _default(in node)
            : Throw.NoStrategyMatched<TResult>();
    }

    /// <summary>Attempts to visit a node; never throws for no-match.</summary>
    /// <param name="node">The node to visit.</param>
    /// <param name="result">The result if a handler or default executed.</param>
    /// <returns><see langword="true"/> if a handler (or default) executed; otherwise <see langword="false"/>.</returns>
    public bool TryVisit(in TBase node, out TResult result)
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

    /// <summary>Creates a new fluent builder for a <see cref="Visitor{TBase, TResult}"/>.</summary>
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

        /// <summary>Builds the immutable, thread-safe visitor.</summary>
        public Visitor<TBase, TResult> Build()
            => _core.Build(
                fallbackDefault: DefaultResult,
                projector: static (predicates, handlers, hasDefault, @default)
                    => new Visitor<TBase, TResult>(predicates, handlers, hasDefault, @default));

        private static bool Is<T>(in TBase node) where T : TBase => node is T;

        private static Handler Wrap<T>(Func<T, TResult> typed) where T : TBase
            => (in node) => typed((T)node!);
    }
}
