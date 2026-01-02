using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.TypeDispatcher;

/// <summary>
/// Type dispatcher (fluent, typed dispatch, action).
/// Maps runtime types deriving from <typeparamref name="TBase"/> to side-effecting actions and
/// executes the first matching action (first-match-wins).
/// </summary>
/// <remarks>
/// <para>
/// This pattern uses runtime type checks to select actions based on the concrete type of the input.
/// It is similar to the Strategy pattern but dispatches based on type rather than explicit strategy selection.
/// </para>
/// <para>
/// <b>Note:</b> This is NOT the Gang of Four Visitor pattern, which uses double dispatch.
/// For true Visitor pattern with double dispatch, see <see cref="PatternKit.Behavioral.Visitor"/> namespace.
/// </para>
/// <para>
/// Use <see cref="Builder.On{T}(System.Action{T})"/> to register actions for concrete types
/// (<c>where T : TBase</c>). Registration order matters: register more specific types before base types.
/// If no action matches, an optional <see cref="Builder.Default(ActionHandler)"/> is used; otherwise
/// <see cref="Dispatch(in TBase)"/> throws and <see cref="TryDispatch(in TBase)"/> returns <see langword="false"/>.
/// </para>
/// <para><b>Thread-safety:</b> built instances are immutable and thread-safe; the builder is not.</para>
/// </remarks>
/// <typeparam name="TBase">The base type for dispatchable elements.</typeparam>
public sealed class ActionTypeDispatcher<TBase>
{
    /// <summary>Predicate to determine if an action applies to a node.</summary>
    /// <param name="node">The node value.</param>
    /// <returns><see langword="true"/> if the action applies; otherwise <see langword="false"/>.</returns>
    public delegate bool Predicate(in TBase node);

    /// <summary>Action that processes a node.</summary>
    /// <param name="node">The node value.</param>
    public delegate void ActionHandler(in TBase node);

    private readonly Predicate[] _predicates;
    private readonly ActionHandler[] _actions;
    private readonly bool _hasDefault;
    private readonly ActionHandler _default;

    private static ActionHandler Noop => static (in _) => { };

    private ActionTypeDispatcher(Predicate[] predicates, ActionHandler[] actions, bool hasDefault, ActionHandler @default)
        => (_predicates, _actions, _hasDefault, _default) = (predicates, actions, hasDefault, @default);

    /// <summary>Dispatches to the first matching action or the default.</summary>
    /// <param name="node">The node to dispatch.</param>
    /// <exception cref="InvalidOperationException">Thrown if no action matches and no default is configured.</exception>
    public void Dispatch(in TBase node)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in node))
            {
                _actions[i](in node);
                return;
            }

        if (_hasDefault)
        {
            _default(in node);
            return;
        }

        Throw.NoStrategyMatched();
    }

    /// <summary>Attempts to dispatch to an action; never throws for no-match.</summary>
    /// <param name="node">The node to dispatch.</param>
    /// <returns><see langword="true"/> if an action (or default) executed; otherwise <see langword="false"/>.</returns>
    public bool TryDispatch(in TBase node)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in node))
            {
                _actions[i](in node);
                return true;
            }

        if (_hasDefault)
        {
            _default(in node);
            return true;
        }

        return false;
    }

    /// <summary>Creates a new fluent builder for an <see cref="ActionTypeDispatcher{TBase}"/>.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder to register type-specific actions and an optional default.</summary>
    public sealed class Builder
    {
        private readonly BranchBuilder<Predicate, ActionHandler> _core = BranchBuilder<Predicate, ActionHandler>.Create();

        /// <summary>Registers an action for nodes of type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">A concrete type assignable to the base type.</typeparam>
        /// <param name="action">The action invoked when the runtime type is <typeparamref name="T"/>.</param>
        public Builder On<T>(Action<T> action) where T : TBase
        {
            _core.Add(Is<T>, Wrap(action));
            return this;
        }

        /// <summary>Sets a default action for nodes with no matching registration.</summary>
        public Builder Default(ActionHandler action)
        {
            _core.Default(action);
            return this;
        }

        /// <summary>Sets a default action using a synchronous delegate without <c>in</c> parameter syntax.</summary>
        public Builder Default(Action<TBase> action)
            => Default((in x) => action(x));

        /// <summary>Builds the immutable, thread-safe type dispatcher.</summary>
        public ActionTypeDispatcher<TBase> Build()
            => _core.Build(
                fallbackDefault: Noop,
                projector: static (predicates, actions, hasDefault, @default)
                    => new ActionTypeDispatcher<TBase>(predicates, actions, hasDefault, @default));

        private static bool Is<T>(in TBase node) where T : TBase => node is T;

        private static ActionHandler Wrap<T>(Action<T> typed) where T : TBase
            => (in node) => typed((T)node!);
    }
}
