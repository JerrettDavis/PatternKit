namespace PatternKit.Application.DomainEvents;

/// <summary>Base contract for domain events emitted by aggregates and application workflows.</summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

/// <summary>Dispatches domain events to registered handlers.</summary>
public interface IDomainEventDispatcher<in TEventBase>
{
    string Name { get; }

    ValueTask<DomainEventDispatchResult> DispatchAsync(TEventBase domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>Typed in-process domain event dispatcher.</summary>
public sealed class DomainEventDispatcher<TEventBase> : IDomainEventDispatcher<TEventBase>
{
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<Func<TEventBase, CancellationToken, ValueTask>>> _handlers;

    private DomainEventDispatcher(
        string name,
        IReadOnlyDictionary<Type, IReadOnlyList<Func<TEventBase, CancellationToken, ValueTask>>> handlers)
    {
        Name = name;
        _handlers = handlers;
    }

    public string Name { get; }

    public static Builder Create(string name)
        => new(name);

    public async ValueTask<DomainEventDispatchResult> DispatchAsync(TEventBase domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent is null)
            throw new ArgumentNullException(nameof(domainEvent));

        cancellationToken.ThrowIfCancellationRequested();
        var eventType = domainEvent.GetType();
        if (!_handlers.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
            return DomainEventDispatchResult.Unhandled(eventType);

        try
        {
            foreach (var handler in handlers)
                await handler(domainEvent, cancellationToken).ConfigureAwait(false);

            return DomainEventDispatchResult.Handled(eventType, handlers.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DomainEventDispatchResult.Failed(eventType, ex);
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly Dictionary<Type, List<Func<TEventBase, CancellationToken, ValueTask>>> _handlers = new();

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Domain Event dispatcher name is required.", nameof(name))
                : name;
        }

        public Builder Handle<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler)
            where TEvent : TEventBase
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(TEvent);
            if (!_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Func<TEventBase, CancellationToken, ValueTask>>();
                _handlers[eventType] = handlers;
            }

            handlers.Add((domainEvent, cancellationToken) =>
            {
                if (domainEvent is not TEvent typedEvent)
                    throw new InvalidOperationException($"Domain event '{domainEvent?.GetType().FullName}' is not assignable to handler event type '{typeof(TEvent).FullName}'.");

                return handler(typedEvent, cancellationToken);
            });
            return this;
        }

        public DomainEventDispatcher<TEventBase> Build()
        {
            var handlers = _handlers.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<Func<TEventBase, CancellationToken, ValueTask>>)pair.Value.ToArray());
            return new(_name, handlers);
        }
    }
}

/// <summary>Result returned after dispatching one domain event.</summary>
public sealed class DomainEventDispatchResult
{
    private DomainEventDispatchResult(Type eventType, DomainEventDispatchStatus status, int handlerCount, Exception? exception)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        Status = status;
        HandlerCount = handlerCount;
        Exception = exception;
    }

    public Type EventType { get; }

    public DomainEventDispatchStatus Status { get; }

    public int HandlerCount { get; }

    public Exception? Exception { get; }

    public bool Succeeded => Status == DomainEventDispatchStatus.Handled;

    public static DomainEventDispatchResult Handled(Type eventType, int handlerCount)
    {
        if (handlerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(handlerCount));

        return new(eventType, DomainEventDispatchStatus.Handled, handlerCount, null);
    }

    public static DomainEventDispatchResult Unhandled(Type eventType)
        => new(eventType, DomainEventDispatchStatus.Unhandled, 0, null);

    public static DomainEventDispatchResult Failed(Type eventType, Exception exception)
        => new(eventType, DomainEventDispatchStatus.Failed, 0, exception ?? throw new ArgumentNullException(nameof(exception)));
}

/// <summary>Dispatch status for one domain event.</summary>
public enum DomainEventDispatchStatus
{
    Handled,
    Unhandled,
    Failed
}
