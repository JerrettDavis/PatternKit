using System.Collections.ObjectModel;

namespace PatternKit.Behavioral.State;

/// <summary>
/// A generic, allocation-light, fluent state machine. Immutable after Build(); thread-safe to share.
/// </summary>
/// <typeparam name="TState">State identifier type (enum, string, record, etc.).</typeparam>
/// <typeparam name="TEvent">Event/trigger type driving transitions.</typeparam>
/// <remarks>
/// Design goals:
/// - Immutable compiled machine; zero allocations on hot path (array iteration).
/// - Predictable: first matching transition in registration order wins.
/// - Extensible: per-state entry/exit hooks, stay/ignore transitions, optional default per-state.
/// - Thread-safe: machine is immutable; callers pass the current state by ref.
/// </remarks>
public sealed class StateMachine<TState, TEvent>
    where TState : notnull
{
    public delegate bool Predicate(in TEvent @event);
    public delegate void Effect(in TEvent @event);
    public delegate void StateHook(in TEvent @event);

    private readonly IEqualityComparer<TState> _cmp;

    private readonly ReadOnlyDictionary<TState, Config> _configs;

    private StateMachine(IEqualityComparer<TState> cmp, ReadOnlyDictionary<TState, Config> configs)
        => (_cmp, _configs) = (cmp, configs);

    /// <summary>
    /// Try to process <paramref name="event"/> against the current <paramref name="state"/>. Returns true if handled.
    /// </summary>
    /// <param name="state">The current state value. Will be updated when a transition occurs.</param>
    /// <param name="event">The incoming event.</param>
    public bool TryTransition(ref TState state, in TEvent @event)
    {
        if (!_configs.TryGetValue(state, out var cfg))
            return false;

        var preds = cfg.Predicates;
        for (int i = 0; i < preds.Length; i++)
        {
            if (preds[i](in @event))
            {
                var tr = cfg.Edges[i];

                // Exit/Effect/Enter order per classic UML
                if (tr.HasNext && !_cmp.Equals(state, tr.Next))
                {
                    // exit hooks of current
                    var exit = cfg.OnExit;
                    for (int j = 0; j < exit.Length; j++) exit[j](in @event);

                    // transition effect
                    tr.Effect?.Invoke(in @event);

                    var next = tr.Next;
                    state = next;

                    if (_configs.TryGetValue(next, out var nextCfg))
                    {
                        var enter = nextCfg.OnEnter;
                        for (int j = 0; j < enter.Length; j++) enter[j](in @event);
                    }
                }
                else
                {
                    // stay/internal transition: effect only
                    tr.Effect?.Invoke(in @event);
                }

                return true;
            }
        }

        // Default (if any)
        if (cfg.HasDefault)
        {
            var tr = cfg.Default;
            if (tr.HasNext && !_cmp.Equals(state, tr.Next))
            {
                var exit = cfg.OnExit;
                for (int j = 0; j < exit.Length; j++) exit[j](in @event);
                tr.Effect?.Invoke(in @event);
                var next = tr.Next;
                state = next;
                if (_configs.TryGetValue(next, out var nextCfg))
                {
                    var enter = nextCfg.OnEnter;
                    for (int j = 0; j < enter.Length; j++) enter[j](in @event);
                }
            }
            else
            {
                tr.Effect?.Invoke(in @event);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Process an event or throw if unhandled in the current state.
    /// </summary>
    public void Transition(ref TState state, in TEvent @event)
    {
        if (!TryTransition(ref state, in @event))
            throw new InvalidOperationException($"Unhandled event '{@event?.ToString() ?? "<null>"}' in state '{state?.ToString() ?? "<null>"}'.");
    }

    internal readonly struct Edge
    {
        public readonly bool HasNext;
        public readonly TState Next;
        public readonly Effect? Effect;
        public Edge(bool hasNext, TState next, Effect? effect)
        {
            HasNext = hasNext;
            Next = next;
            Effect = effect;
        }
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
        public Config(Predicate[] p, Edge[] t, StateHook[] onEnter, StateHook[] onExit, bool hasDef, Edge def)
            => (Predicates, Edges, OnEnter, OnExit, HasDefault, Default) = (p, t, onEnter, onExit, hasDef, def);
    }

    public sealed class Builder
    {
        private readonly Dictionary<TState, StateBuilder> _states = new();
        private IEqualityComparer<TState> _cmp = EqualityComparer<TState>.Default;

        /// <summary>Provide a custom equality comparer for TState (e.g., case-insensitive string states).</summary>
        public Builder Comparer(IEqualityComparer<TState> comparer)
        {
            _cmp = comparer ?? EqualityComparer<TState>.Default;
            return this;
        }

        /// <summary>Create or get a builder for a specific state.</summary>
        public StateBuilder State(TState state)
        {
            if (!_states.TryGetValue(state, out var b))
                _states[state] = b = new StateBuilder(state, this);
            return b;
        }

        /// <summary>Convenience to configure a state in-line.</summary>
        public Builder InState(TState state, Func<StateBuilder, StateBuilder> configure)
        {
            var b = State(state);
            configure(b);
            return this;
        }

        public StateMachine<TState, TEvent> Build()
        {
            var cfg = new Dictionary<TState, Config>(_states.Count, _cmp);
            foreach (var kvp in _states)
            {
                var key = kvp.Key;
                var sb = kvp.Value;
                var t = sb.BuildTransitions();
                cfg[key] = new Config(
                    t.preds,
                    t.edges,
                    sb._onEnter.ToArray(),
                    sb._onExit.ToArray(),
                    t.hasDefault,
                    t.def);
            }
            return new StateMachine<TState, TEvent>(_cmp, new ReadOnlyDictionary<TState, Config>(cfg));
        }

        // ----- Nested state builder -----
        public sealed class StateBuilder
        {
            internal readonly TState _state;
            private readonly Builder _owner;
            private readonly List<Predicate> _predicates = new(4);
            private readonly List<Edge> _edges = new(4);
            internal readonly List<StateHook> _onEnter = new(2);
            internal readonly List<StateHook> _onExit = new(2);
            private bool _hasDefault;
            private Edge _default;

            internal StateBuilder(TState state, Builder owner) => (_state, _owner) = (state, owner);

            public WhenBuilder When(Predicate predicate) => new(this, predicate);

            /// <summary>Default transition used when no predicate matches.</summary>
            public ThenBuilder Otherwise() => new(this, static (in _) => true);

            /// <summary>Register an entry hook invoked after entering this state.</summary>
            public StateBuilder OnEnter(StateHook hook)
            { if (hook is not null) _onEnter.Add(hook); return this; }

            /// <summary>Register an exit hook invoked before leaving this state.</summary>
            public StateBuilder OnExit(StateHook hook)
            { if (hook is not null) _onExit.Add(hook); return this; }

            /// <summary>Fluent return to the machine builder.</summary>
            public Builder End() => _owner;

            internal (Predicate[] preds, Edge[] edges, bool hasDefault, Edge def) BuildTransitions()
            {
                var preds = _predicates.ToArray();
                var edges = _edges.ToArray();
                return (preds, edges, _hasDefault, _default);
            }

            public readonly struct WhenBuilder
            {
                private readonly StateBuilder _owner;
                private readonly Predicate _pred;
                internal WhenBuilder(StateBuilder owner, Predicate pred) => (_owner, _pred) = (owner, pred);
                public ThenBuilder Permit(TState next) => new(_owner, _pred) { _next = next, _hasNext = true };
                public ThenBuilder Stay() => new(_owner, _pred) { _hasNext = false };
            }

            public struct ThenBuilder
            {
                private readonly StateBuilder _owner;
                private readonly Predicate _pred;
                internal bool _hasNext;
                internal TState _next = default!;
                internal ThenBuilder(StateBuilder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

                /// <summary>Specify the next state for this rule (used with Otherwise as well).</summary>
                public ThenBuilder Permit(TState next) { _hasNext = true; _next = next; return this; }
                /// <summary>Specify this rule as a stay/internal transition.</summary>
                public ThenBuilder Stay() { _hasNext = false; return this; }

                /// <summary>Attach a side-effect to run during the transition (between exit and entry).</summary>
                public StateBuilder Do(Effect effect)
                {
                    var tr = _hasNext ? Edge.To(_next, effect) : Edge.Stay(effect);
                    _owner._predicates.Add(_pred);
                    _owner._edges.Add(tr);
                    return _owner;
                }

                /// <summary>No side-effect.</summary>
                public StateBuilder End()
                {
                    var tr = _hasNext ? Edge.To(_next, null) : Edge.Stay(null);
                    _owner._predicates.Add(_pred);
                    _owner._edges.Add(tr);
                    return _owner;
                }

                /// <summary>Set as default instead of a conditional rule.</summary>
                public StateBuilder AsDefault()
                {
                    var tr = _hasNext ? Edge.To(_next, null) : Edge.Stay(null);
                    _owner._hasDefault = true;
                    _owner._default = tr;
                    return _owner;
                }
            }
        }
    }

    /// <summary>Create a new builder.</summary>
    public static Builder Create() => new();
}
