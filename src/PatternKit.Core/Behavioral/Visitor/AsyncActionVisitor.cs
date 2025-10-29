using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Visitor (fluent, typed dispatch, async action).
/// Maps runtime types deriving from <typeparamref name="TBase"/> to asynchronous actions and executes
/// the first matching action (first-match-wins).
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="Builder.On{T}(System.Func{T,System.Threading.CancellationToken,System.Threading.Tasks.ValueTask})"/>
/// to register actions for concrete types (<c>where T : TBase</c>). Registration order matters: register more specific
/// types before base types. If no action matches, an optional <see cref="Builder.Default(ActionHandler)"/> is used;
/// otherwise <see cref="VisitAsync(TBase,System.Threading.CancellationToken)"/> throws and
/// <see cref="TryVisitAsync(TBase,System.Threading.CancellationToken)"/> returns <see langword="false"/>.
/// </para>
/// <para><b>Thread-safety:</b> built instances are immutable and thread-safe; the builder is not.</para>
/// </remarks>
/// <typeparam name="TBase">The base type for visitable elements.</typeparam>
public sealed class AsyncActionVisitor<TBase>
{
    /// <summary>Asynchronous predicate to determine if an action applies.</summary>
    public delegate ValueTask<bool> Predicate(TBase node, CancellationToken ct);

    /// <summary>Asynchronous action that processes a node.</summary>
    public delegate ValueTask ActionHandler(TBase node, CancellationToken ct);

    private readonly Predicate[] _predicates;
    private readonly ActionHandler[] _actions;
    private readonly bool _hasDefault;
    private readonly ActionHandler _default;

    private static ActionHandler Noop => static (_, _) => default;

    private AsyncActionVisitor(Predicate[] predicates, ActionHandler[] actions, bool hasDefault, ActionHandler @default)
        => (_predicates, _actions, _hasDefault, _default) = (predicates, actions, hasDefault, @default);

    /// <summary>Visits a node and executes the first matching action or the default.</summary>
    public async ValueTask VisitAsync(TBase node, CancellationToken ct = default)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (await predicates[i](node, ct).ConfigureAwait(false))
            {
                await _actions[i](node, ct).ConfigureAwait(false);
                return;
            }

        if (_hasDefault)
        {
            await _default(node, ct).ConfigureAwait(false);
            return;
        }

        Throw.NoStrategyMatched();
    }

    /// <summary>Attempts to visit a node; never throws for no-match.</summary>
    public async ValueTask<bool> TryVisitAsync(TBase node, CancellationToken ct = default)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (await predicates[i](node, ct).ConfigureAwait(false))
            {
                await _actions[i](node, ct).ConfigureAwait(false);
                return true;
            }

        if (_hasDefault)
        {
            await _default(node, ct).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    /// <summary>Creates a new fluent builder for an <see cref="AsyncActionVisitor{TBase}"/>.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder to register type-specific async actions and an optional default.</summary>
    public sealed class Builder
    {
        private readonly BranchBuilder<Predicate, ActionHandler> _core = BranchBuilder<Predicate, ActionHandler>.Create();

        /// <summary>Registers an async action for nodes of type <typeparamref name="T"/>.</summary>
        public Builder On<T>(Func<T, CancellationToken, ValueTask> action) where T : TBase
        {
            _core.Add(Is<T>, Wrap(action));
            return this;
        }

        /// <summary>Registers a sync action for nodes of type <typeparamref name="T"/>.</summary>
        public Builder On<T>(Action<T> action) where T : TBase
            => On<T>((x, _) => { action(x); return default; });

        /// <summary>Sets an async default action for nodes with no matching registration.</summary>
        public Builder Default(ActionHandler action)
        {
            _core.Default(action);
            return this;
        }

        /// <summary>Sets a sync default action for nodes with no matching registration.</summary>
        public Builder Default(Action<TBase> action)
            => Default((x, _) => { action(x); return default; });

        /// <summary>Builds the immutable, thread-safe visitor.</summary>
        public AsyncActionVisitor<TBase> Build()
            => _core.Build(
                fallbackDefault: Noop,
                projector: static (predicates, actions, hasDefault, @default)
                    => new AsyncActionVisitor<TBase>(predicates, actions, hasDefault, @default));

        private static ValueTask<bool> Is<T>(TBase node, CancellationToken _) where T : TBase
            => new(node is T);

        private static ActionHandler Wrap<T>(Func<T, CancellationToken, ValueTask> typed) where T : TBase
            => (node, ct) => typed((T)node!, ct);
    }
}
