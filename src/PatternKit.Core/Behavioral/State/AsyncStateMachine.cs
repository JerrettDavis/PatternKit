using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.State;

/// <summary>
/// An asynchronous, fluent state machine. Immutable after Build(); safe to share across threads.
/// </summary>
/// <typeparam name="TState">State identifier type (enum, string, record, etc.).</typeparam>
/// <typeparam name="TEvent">Event/trigger type driving transitions.</typeparam>
public sealed class AsyncStateMachine<TState, TEvent>
    where TState : notnull
{
    /// <summary>Asynchronous predicate determining if a transition applies.</summary>
    public delegate ValueTask<bool> Predicate(TEvent @event, CancellationToken ct);
    /// <summary>Asynchronous effect to run during a transition.</summary>
    public delegate ValueTask Effect(TEvent @event, CancellationToken ct);
    /// <summary>Asynchronous hook invoked on state entry/exit.</summary>
    public delegate ValueTask StateHook(TEvent @event, CancellationToken ct);

    private readonly IEqualityComparer<TState> _cmp;
    private readonly ReadOnlyDictionary<TState, Config> _configs;

    private AsyncStateMachine(IEqualityComparer<TState> cmp, ReadOnlyDictionary<TState, Config> configs)
        => (_cmp, _configs) = (cmp, configs);

    /// <summary>
    /// Tries to process an event in the given state.
    /// Returns (handled, newState). If no rule handled the event, handled is false and newState == input state.
    /// </summary>
    public async ValueTask<(bool handled, TState state)> TryTransitionAsync(TState state, TEvent @event, CancellationToken ct = default)
    {
        if (!_configs.TryGetValue(state, out var cfg))
            return (false, state);

        var current = state;
        var preds = cfg.Predicates;
        for (int i = 0; i < preds.Length; i++)
        {
            if (await preds[i](@event, ct).ConfigureAwait(false))
            {
                var tr = cfg.Edges[i];
                if (tr.HasNext && !_cmp.Equals(current, tr.Next))
                {
                    var exit = cfg.OnExit;
                    for (int j = 0; j < exit.Length; j++)
                        await exit[j](@event, ct).ConfigureAwait(false);

                    if (tr.Effect is not null)
                        await tr.Effect(@event, ct).ConfigureAwait(false);

                    var next = tr.Next;
                    current = next;

                    if (_configs.TryGetValue(next, out var nextCfg))
                    {
                        var enter = nextCfg.OnEnter;
                        for (int j = 0; j < enter.Length; j++)
                            await enter[j](@event, ct).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (tr.Effect is not null)
                        await tr.Effect(@event, ct).ConfigureAwait(false);
                }
                return (true, current);
            }
        }

        if (cfg.HasDefault)
        {
            var tr = cfg.Default;
            if (tr.HasNext && !_cmp.Equals(current, tr.Next))
            {
                var exit = cfg.OnExit;
                for (int j = 0; j < exit.Length; j++)
                    await exit[j](@event, ct).ConfigureAwait(false);
                if (tr.Effect is not null)
                    await tr.Effect(@event, ct).ConfigureAwait(false);
                var next = tr.Next;
                current = next;
                if (_configs.TryGetValue(next, out var nextCfg))
                {
                    var enter = nextCfg.OnEnter;
                    for (int j = 0; j < enter.Length; j++)
                        await enter[j](@event, ct).ConfigureAwait(false);
                }
            }
            else
            {
                if (tr.Effect is not null)
                    await tr.Effect(@event, ct).ConfigureAwait(false);
            }
            return (true, current);
        }

        return (false, current);
    }

    /// <summary>Processes an event or throws if unhandled. Returns the resulting state.</summary>
    public async ValueTask<TState> TransitionAsync(TState state, TEvent @event, CancellationToken ct = default)
    {
        var (handled, next) = await TryTransitionAsync(state, @event, ct).ConfigureAwait(false);
        if (!handled)
            throw new InvalidOperationException($"Unhandled event '{@event?.ToString() ?? "<null>"}' in state '{state?.ToString() ?? "<null>"}'.");
        return next;
    }

    internal readonly struct Edge
    {
        public readonly bool HasNext;
        public readonly TState Next;
        public readonly Effect? Effect;
        public Edge(bool hasNext, TState next, Effect? effect)
        { HasNext = hasNext; Next = next; Effect = effect; }
        public static Edge Stay(Effect? effect) => new(false, default!, effect);
        public static Edge To(TState next, Effect? effect) => new(true, next, effect);
    }

    private sealed class Config
    {
        public readonly Predicate[] Predicates;
        public readonly Edge[] Edges;
        public readonly StateHook[] OnEnter;
        public readonly StateHook[] OnExit;
        public readonly bool HasDefault;
        public readonly Edge Default;
        public Config(Predicate[] p, Edge[] e, StateHook[] onEnter, StateHook[] onExit, bool hasDef, Edge def)
            => (Predicates, Edges, OnEnter, OnExit, HasDefault, Default) = (p, e, onEnter, onExit, hasDef, def);
    }

    public sealed class Builder
    {
        private readonly Dictionary<TState, StateBuilder> _states = new();
        private IEqualityComparer<TState> _cmp = EqualityComparer<TState>.Default;

        public Builder Comparer(IEqualityComparer<TState> comparer)
        { _cmp = comparer ?? EqualityComparer<TState>.Default; return this; }

        public StateBuilder State(TState state)
        {
            if (!_states.TryGetValue(state, out var b))
                _states[state] = b = new StateBuilder(state, this);
            return b;
        }

        public Builder InState(TState state, Func<StateBuilder, StateBuilder> configure)
        { var b = State(state); configure(b); return this; }

        public AsyncStateMachine<TState, TEvent> Build()
        {
            var dict = new Dictionary<TState, Config>(_states.Count, _cmp);
            foreach (var kv in _states)
            {
                var key = kv.Key; var sb = kv.Value; var t = sb.BuildTransitions();
                dict[key] = new Config(t.preds, t.edges, sb._onEnter.ToArray(), sb._onExit.ToArray(), t.hasDefault, t.def);
            }
            return new AsyncStateMachine<TState, TEvent>(_cmp, new ReadOnlyDictionary<TState, Config>(dict));
        }

        public sealed class StateBuilder
        {
            private readonly Builder _owner;
            internal readonly TState _state;
            private readonly List<Predicate> _preds = new(4);
            private readonly List<Edge> _edges = new(4);
            internal readonly List<StateHook> _onEnter = new(2);
            internal readonly List<StateHook> _onExit = new(2);
            private bool _hasDefault;
            private Edge _default;

            internal StateBuilder(TState state, Builder owner) => (_state, _owner) = (state, owner);

            public WhenBuilder When(Predicate pred) => new(this, pred);
            public ThenBuilder Otherwise() => new(this, static (_, _) => new ValueTask<bool>(true));
            public StateBuilder OnEnter(StateHook hook) { if (hook is not null) _onEnter.Add(hook); return this; }
            public StateBuilder OnExit(StateHook hook) { if (hook is not null) _onExit.Add(hook); return this; }
            public Builder End() => _owner;

            internal (Predicate[] preds, Edge[] edges, bool hasDefault, Edge def) BuildTransitions()
                => (_preds.ToArray(), _edges.ToArray(), _hasDefault, _default);

            public readonly struct WhenBuilder
            {
                private readonly StateBuilder _owner;
                private readonly Predicate _pred;
                internal WhenBuilder(StateBuilder owner, Predicate pred) => (_owner, _pred) = (owner, pred);
                public ThenBuilder Permit(TState next) => new(_owner, _pred) { _hasNext = true, _next = next };
                public ThenBuilder Stay() => new(_owner, _pred) { _hasNext = false };
            }

            public struct ThenBuilder
            {
                private readonly StateBuilder _owner;
                private readonly Predicate _pred;
                internal bool _hasNext;
                internal TState _next = default!;
                internal ThenBuilder(StateBuilder owner, Predicate pred) => (_owner, _pred) = (owner, pred);
                public ThenBuilder Permit(TState next) { _hasNext = true; _next = next; return this; }
                public ThenBuilder Stay() { _hasNext = false; return this; }

                public StateBuilder Do(Effect effect)
                {
                    var e = _hasNext ? Edge.To(_next, effect) : Edge.Stay(effect);
                    _owner._preds.Add(_pred);
                    _owner._edges.Add(e);
                    return _owner;
                }

                public StateBuilder End()
                {
                    var e = _hasNext ? Edge.To(_next, null) : Edge.Stay(null);
                    _owner._preds.Add(_pred);
                    _owner._edges.Add(e);
                    return _owner;
                }

                public StateBuilder AsDefault()
                {
                    var e = _hasNext ? Edge.To(_next, null) : Edge.Stay(null);
                    _owner._hasDefault = true;
                    _owner._default = e;
                    return _owner;
                }
            }
        }
    }

    /// <summary>Create a new builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Builder Create() => new();
}
