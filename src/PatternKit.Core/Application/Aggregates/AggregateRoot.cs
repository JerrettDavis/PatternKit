namespace PatternKit.Application.Aggregates;

/// <summary>
/// Base class for aggregate roots that protect invariants through command decisions and domain events.
/// </summary>
public abstract class AggregateRoot<TId, TEvent>
    where TId : notnull
{
    private readonly List<TEvent> _uncommittedEvents = [];

    protected AggregateRoot(TId id)
    {
        Id = id;
    }

    public TId Id { get; }

    public long Version { get; private set; }

    public IReadOnlyList<TEvent> UncommittedEvents => _uncommittedEvents;

    public IReadOnlyList<TEvent> DequeueUncommittedEvents()
    {
        var events = _uncommittedEvents.ToArray();
        _uncommittedEvents.Clear();
        return events;
    }

    public void MarkCommitted() => _uncommittedEvents.Clear();

    protected void Raise(TEvent domainEvent, Action<TEvent> apply)
    {
        if (apply is null)
            throw new ArgumentNullException(nameof(apply));

        apply(domainEvent);
        _uncommittedEvents.Add(domainEvent);
        Version++;
    }

    protected void Replay(TEvent domainEvent, Action<TEvent> apply)
    {
        if (apply is null)
            throw new ArgumentNullException(nameof(apply));

        apply(domainEvent);
        Version++;
    }
}

/// <summary>
/// Fluent command handler for deciding aggregate events and applying them atomically.
/// </summary>
public sealed class AggregateCommandHandler<TAggregate, TCommand, TEvent>
{
    private readonly Func<TAggregate, TCommand, IEnumerable<TEvent>> _decide;
    private readonly Action<TAggregate, TEvent> _apply;

    private AggregateCommandHandler(
        string name,
        Func<TAggregate, TCommand, IEnumerable<TEvent>> decide,
        Action<TAggregate, TEvent> apply)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Handler name is required.", nameof(name))
            : name;
        _decide = decide ?? throw new ArgumentNullException(nameof(decide));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    }

    public string Name { get; }

    public static AggregateCommandHandler<TAggregate, TCommand, TEvent> Create(
        string name,
        Func<TAggregate, TCommand, IEnumerable<TEvent>> decide,
        Action<TAggregate, TEvent> apply)
        => new(name, decide, apply);

    public AggregateCommandResult<TEvent> Execute(TAggregate aggregate, TCommand command)
    {
        if (aggregate is null)
            throw new ArgumentNullException(nameof(aggregate));
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var events = _decide(aggregate, command)?.ToArray() ?? Array.Empty<TEvent>();
        foreach (var domainEvent in events)
            _apply(aggregate, domainEvent);

        return new AggregateCommandResult<TEvent>(Name, events);
    }
}

public sealed class AggregateCommandResult<TEvent>
{
    public AggregateCommandResult(string handler, IReadOnlyList<TEvent> events)
    {
        Handler = handler;
        Events = events;
    }

    public string Handler { get; }

    public IReadOnlyList<TEvent> Events { get; }

    public bool HasChanges => Events.Count > 0;
}
