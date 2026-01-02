using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Type-based dispatcher (fluent, typed dispatch, action).
/// Maps runtime types deriving from <typeparamref name="TBase"/> to side-effecting actions and
/// executes the first matching action (first-match-wins).
/// </summary>
/// <remarks>
/// <para>
/// <b>DEPRECATION NOTICE:</b> This class has been renamed to <see cref="TypeDispatcher.ActionTypeDispatcher{TBase}"/>
/// to better reflect its actual behavior. This class uses runtime type checking (Strategy-like pattern),
/// NOT the Gang of Four Visitor pattern with double dispatch.
/// </para>
/// <para>
/// Use <see cref="Builder.On{T}(System.Action{T})"/> to register actions for concrete types
/// (<c>where T : TBase</c>). Registration order matters: register more specific types before base types.
/// If no action matches, an optional <see cref="Builder.Default(ActionHandler)"/> is used; otherwise
/// <see cref="Visit(in TBase)"/> throws and <see cref="TryVisit(in TBase)"/> returns <see langword="false"/>.
/// </para>
/// <para><b>Thread-safety:</b> built instances are immutable and thread-safe; the builder is not.</para>
/// </remarks>
/// <typeparam name="TBase">The base type for visitable elements.</typeparam>
[Obsolete("Use ActionTypeDispatcher<TBase> instead. This class has been renamed to better reflect its behavior (type-based dispatch, not GoF Visitor pattern).")]
public sealed class ActionVisitor<TBase>
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

    private ActionVisitor(Predicate[] predicates, ActionHandler[] actions, bool hasDefault, ActionHandler @default)
        => (_predicates, _actions, _hasDefault, _default) = (predicates, actions, hasDefault, @default);

    /// <summary>Visits a node and executes the first matching action or the default.</summary>
    /// <param name="node">The node to visit.</param>
    /// <exception cref="InvalidOperationException">Thrown if no action matches and no default is configured.</exception>
    public void Visit(in TBase node)
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

    /// <summary>Attempts to visit a node; never throws for no-match.</summary>
    /// <param name="node">The node to visit.</param>
    /// <returns><see langword="true"/> if an action (or default) executed; otherwise <see langword="false"/>.</returns>
    public bool TryVisit(in TBase node)
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

    /// <summary>Creates a new fluent builder for an <see cref="ActionVisitor{TBase}"/>.</summary>
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

        /// <summary>Builds the immutable, thread-safe visitor.</summary>
        public ActionVisitor<TBase> Build()
            => _core.Build(
                fallbackDefault: Noop,
                projector: static (predicates, actions, hasDefault, @default)
                    => new ActionVisitor<TBase>(predicates, actions, hasDefault, @default));

        private static bool Is<T>(in TBase node) where T : TBase => node is T;

        private static ActionHandler Wrap<T>(Action<T> typed) where T : TBase
            => (in node) => typed((T)node!);
    }
}
