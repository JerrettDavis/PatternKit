using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.Chain;

/// <summary>
/// Middleware-style chain. Each handler decides to call <c>next</c> or short-circuit.
/// </summary>
public sealed class ActionChain<TCtx>
{
    public delegate void Next(in TCtx ctx);

    public delegate void Handler(in TCtx ctx, Next next);

    public delegate bool Predicate(in TCtx ctx);

    private readonly Next _entry;

    private ActionChain(Next entry) => _entry = entry;

    /// <summary>Executes the chain.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(in TCtx ctx) => _entry(in ctx);

    public sealed class Builder
    {
        private readonly List<Handler> _handlers = new(8);
        private Handler? _tail;

        /// <summary>Adds middleware.</summary>
        public Builder Use(Handler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        /// <summary>Conditionally run middleware: if predicate false, continues automatically.</summary>
        public WhenBuilder When(Predicate predicate) => new(this, predicate);

        /// <summary>Sets terminal tail (runs if no earlier short-circuit stops the chain).</summary>
        public Builder Finally(Handler tail)
        {
            _tail = tail;
            return this;
        }

        /// <summary>Builds an immutable chain by composing handlers into a single delegate.</summary>
        public ActionChain<TCtx> Build()
        {
            // terminal "no-op" continuation
            Next next = static (in _) => { };

            if (_tail is not null)
            {
                var t = _tail;
                var prev = next;
                next = (in c) => t(in c, prev);
            }

            for (var i = _handlers.Count - 1; i >= 0; i--)
            {
                var h = _handlers[i];
                var prev = next;
                next = (in c) => h(in c, prev);
            }

            return new ActionChain<TCtx>(next);
        }

        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;
            internal WhenBuilder(Builder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

            public Builder Do(Handler handler)
            {
                var pred = _pred; // avoid capturing 'this'
                _owner.Use((in c, next) =>
                {
                    if (pred(in c)) handler(in c, next);
                    else next(in c);
                });
                return _owner;
            }

            public Builder ThenStop(Action<TCtx> action)
            {
                var pred = _pred;
                _owner.Use((in c, next) =>
                {
                    if (pred(in c))
                    {
                        action(c);
                        return;
                    }

                    next(in c);
                });
                return _owner;
            }

            public Builder ThenContinue(Action<TCtx> action)
            {
                var pred = _pred;
                _owner.Use((in c, next) =>
                {
                    if (pred(in c)) action(c);
                    next(in c);
                });
                return _owner;
            }
        }
    }

    /// <summary>Starts a new <see cref="Builder"/>.</summary>
    public static Builder Create() => new();
}