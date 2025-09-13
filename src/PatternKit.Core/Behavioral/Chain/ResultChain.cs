using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.Chain;

/// <summary>
/// Chain that may produce a value. Each handler can return a result or delegate to <c>next</c>.
/// </summary>
public sealed class ResultChain<TIn, TOut>
{
    public delegate bool Next(in TIn input, out TOut? result);

    public delegate bool TryHandler(in TIn input, out TOut? result, Next next);

    public delegate bool Predicate(in TIn input);

    private readonly Next _entry;

    private ResultChain(Next entry) => _entry = entry;

    /// <summary>Executes the chain. Returns true if any handler produced a result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Execute(in TIn input, out TOut? result) => _entry(in input, out result);

    public sealed class Builder
    {
        private readonly List<TryHandler> _handlers = new(8);
        private TryHandler? _tail;

        public Builder Use(TryHandler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        public WhenBuilder When(Predicate predicate) => new(this, predicate);

        /// <summary>Sets a terminal fallback (e.g., NotFound/Default).</summary>
        public Builder Finally(TryHandler tail)
        {
            _tail = tail;
            return this;
        }

        public ResultChain<TIn, TOut> Build()
        {
            // terminal: no result
            Next next = static (in _, out r) =>
            {
                r = default;
                return false;
            };

            if (_tail is not null)
            {
                var t = _tail;
                var prev = next;
                next = (in x, out r) => t(in x, out r, prev);
            }

            for (var i = _handlers.Count - 1; i >= 0; i--)
            {
                var h = _handlers[i];
                var prev = next;
                next = (in x, out r) => h(in x, out r, prev);
            }

            return new ResultChain<TIn, TOut>(next);
        }


        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;
            internal WhenBuilder(Builder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

            public Builder Do(TryHandler handler)
            {
                var pred = _pred;
                _owner.Use((in x, out r, next)
                    => pred(in x)
                        ? handler(in x, out r, next)
                        : next(in x, out r));
                return _owner;
            }

            public Builder Then(Func<TIn, TOut> produce)
            {
                var pred = _pred;
                _owner.Use((in x, out r, next) =>
                {
                    if (!pred(in x))
                        return next(in x, out r);

                    r = produce(x);
                    return true;
                });
                return _owner;
            }
        }
    }

    public static Builder Create() => new();
}