namespace PatternKit.Messaging.Sagas;

/// <summary>
/// In-process saga/process manager that routes typed messages to state transition handlers.
/// </summary>
public sealed class Saga<TState>
{
    /// <summary>Predicate used to decide whether a typed saga step should handle a message.</summary>
    public delegate bool StepPredicate<TMessage>(TState state, Message<TMessage> message, MessageContext context);

    /// <summary>Handler used to transition saga state for a typed message.</summary>
    public delegate TState StepHandler<TMessage>(TState state, Message<TMessage> message, MessageContext context);

    /// <summary>Predicate used to decide whether saga state is complete.</summary>
    public delegate bool CompletionPredicate(TState state);

    private readonly Step[] _steps;
    private readonly CompletionPredicate _completeWhen;

    private Saga(Step[] steps, CompletionPredicate completeWhen)
        => (_steps, _completeWhen) = (steps, completeWhen);

    /// <summary>
    /// Handles <paramref name="message"/> and returns the resulting saga state.
    /// </summary>
    public SagaResult<TState> Handle<TMessage>(
        TState state,
        Message<TMessage> message,
        MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var current = state;
        var matched = false;
        foreach (var step in _steps)
        {
            if (step.MessageType != typeof(TMessage))
                continue;

            var typed = (Step<TMessage>)step;
            if (!typed.Predicate(current, message, effectiveContext))
                continue;

            current = typed.Handler(current, message, effectiveContext);
            matched = true;
        }

        return new SagaResult<TState>(current, matched, _completeWhen(current));
    }

    /// <summary>Creates a new saga builder.</summary>
    public static Builder Create() => new();

    private abstract class Step
    {
        protected Step(Type messageType) => MessageType = messageType;

        internal Type MessageType { get; }
    }

    private sealed class Step<TMessage> : Step
    {
        internal Step(StepPredicate<TMessage> predicate, StepHandler<TMessage> handler)
            : base(typeof(TMessage))
            => (Predicate, Handler) = (predicate, handler);

        internal StepPredicate<TMessage> Predicate { get; }

        internal StepHandler<TMessage> Handler { get; }
    }

    /// <summary>Fluent builder for <see cref="Saga{TState}"/>.</summary>
    public sealed class Builder
    {
        private readonly List<Step> _steps = new();
        private CompletionPredicate _completeWhen = static _ => false;

        /// <summary>Adds a typed message step.</summary>
        public WhenBuilder<TMessage> On<TMessage>()
            => new(this, static (_, _, _) => true);

        /// <summary>Adds a typed message step with a guard predicate.</summary>
        public WhenBuilder<TMessage> When<TMessage>(StepPredicate<TMessage> predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return new WhenBuilder<TMessage>(this, predicate);
        }

        /// <summary>Sets the saga completion predicate.</summary>
        public Builder CompleteWhen(CompletionPredicate predicate)
        {
            _completeWhen = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        /// <summary>Builds an immutable saga process manager.</summary>
        public Saga<TState> Build() => new(_steps.ToArray(), _completeWhen);

        /// <summary>Fluent step continuation.</summary>
        public sealed class WhenBuilder<TMessage>
        {
            private readonly Builder _owner;
            private readonly StepPredicate<TMessage> _predicate;

            internal WhenBuilder(Builder owner, StepPredicate<TMessage> predicate)
                => (_owner, _predicate) = (owner, predicate);

            /// <summary>Adds the state transition handler for this message type.</summary>
            public Builder Then(StepHandler<TMessage> handler)
            {
                if (handler is null)
                    throw new ArgumentNullException(nameof(handler));

                _owner._steps.Add(new Step<TMessage>(_predicate, handler));
                return _owner;
            }
        }
    }
}
