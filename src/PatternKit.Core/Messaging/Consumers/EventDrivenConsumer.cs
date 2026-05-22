namespace PatternKit.Messaging.Consumers;

/// <summary>Push-based consumer that handles messages when application code delivers them.</summary>
public sealed class EventDrivenConsumer<TPayload>
{
    public delegate EventDrivenConsumerHandlerResult Handler(Message<TPayload> message, MessageContext context);

    private readonly IReadOnlyList<HandlerRegistration> _handlers;
    private readonly EventDrivenConsumerErrorPolicy _errorPolicy;

    private EventDrivenConsumer(string name, IReadOnlyList<HandlerRegistration> handlers, EventDrivenConsumerErrorPolicy errorPolicy)
        => (Name, _handlers, _errorPolicy) = (name, handlers, errorPolicy);

    public string Name { get; }

    public EventDrivenConsumerResult<TPayload> Accept(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var failures = new List<EventDrivenConsumerHandlerResult>();
        var invoked = 0;

        foreach (var registration in _handlers)
        {
            invoked++;
            EventDrivenConsumerHandlerResult result;
            try
            {
                result = registration.Handler(message, effectiveContext);
            }
            catch (Exception ex)
            {
                result = EventDrivenConsumerHandlerResult.Failure(registration.Name, ex.Message, ex);
            }

            if (!result.Succeeded)
            {
                failures.Add(result);
                if (_errorPolicy == EventDrivenConsumerErrorPolicy.StopOnFirstFailure)
                    break;
            }
        }

        return new EventDrivenConsumerResult<TPayload>(Name, message, invoked, failures.AsReadOnly());
    }

    public static Builder Create(string name = "event-driven-consumer") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<HandlerRegistration> _handlers = new();
        private EventDrivenConsumerErrorPolicy _errorPolicy = EventDrivenConsumerErrorPolicy.StopOnFirstFailure;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Event-driven consumer name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder Handle(string handlerName, Handler handler)
        {
            if (string.IsNullOrWhiteSpace(handlerName))
                throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(handlerName));

            _handlers.Add(new HandlerRegistration(handlerName, handler ?? throw new ArgumentNullException(nameof(handler))));
            return this;
        }

        public Builder Handle(string handlerName, Action<Message<TPayload>, MessageContext> handler)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            return Handle(handlerName, (message, context) =>
            {
                handler(message, context);
                return EventDrivenConsumerHandlerResult.Success(handlerName);
            });
        }

        public Builder OnError(EventDrivenConsumerErrorPolicy policy)
        {
            _errorPolicy = policy;
            return this;
        }

        public EventDrivenConsumer<TPayload> Build()
        {
            if (_handlers.Count == 0)
                throw new InvalidOperationException("Event-driven consumer requires at least one handler.");

            return new(_name, _handlers.ToArray(), _errorPolicy);
        }
    }

    private sealed class HandlerRegistration
    {
        public HandlerRegistration(string name, Handler handler)
            => (Name, Handler) = (name, handler);

        public string Name { get; }

        public Handler Handler { get; }
    }
}

public enum EventDrivenConsumerErrorPolicy
{
    StopOnFirstFailure,
    Continue
}

public sealed class EventDrivenConsumerHandlerResult
{
    private EventDrivenConsumerHandlerResult(string handlerName, bool succeeded, string? reason, Exception? exception)
        => (HandlerName, Succeeded, Reason, Exception) = (handlerName, succeeded, reason, exception);

    public string HandlerName { get; }

    public bool Succeeded { get; }

    public string? Reason { get; }

    public Exception? Exception { get; }

    public static EventDrivenConsumerHandlerResult Success(string handlerName)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
            throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(handlerName));

        return new(handlerName, true, null, null);
    }

    public static EventDrivenConsumerHandlerResult Failure(string handlerName, string reason, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
            throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(handlerName));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason cannot be null, empty, or whitespace.", nameof(reason));

        return new(handlerName, false, reason, exception);
    }
}

public sealed class EventDrivenConsumerResult<TPayload>
{
    internal EventDrivenConsumerResult(
        string consumerName,
        Message<TPayload> message,
        int handlerCount,
        IReadOnlyList<EventDrivenConsumerHandlerResult> failures)
        => (ConsumerName, Message, HandlerCount, Failures) = (consumerName, message, handlerCount, failures);

    public string ConsumerName { get; }

    public Message<TPayload> Message { get; }

    public int HandlerCount { get; }

    public IReadOnlyList<EventDrivenConsumerHandlerResult> Failures { get; }

    public bool Accepted => Failures.Count == 0;
}
