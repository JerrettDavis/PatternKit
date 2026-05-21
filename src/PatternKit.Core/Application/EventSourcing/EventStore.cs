namespace PatternKit.Application.EventSourcing;

public interface IEventStore<TEvent, TStreamId>
    where TStreamId : notnull
{
    string Name { get; }

    ValueTask<EventStoreAppendResult> AppendAsync(TStreamId streamId, long expectedVersion, IEnumerable<TEvent> events, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<StoredEvent<TEvent, TStreamId>>> ReadStreamAsync(TStreamId streamId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryEventStore<TEvent, TStreamId> : IEventStore<TEvent, TStreamId>
    where TStreamId : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TStreamId, List<StoredEvent<TEvent, TStreamId>>> _streams;

    private InMemoryEventStore(string name, IEqualityComparer<TStreamId>? comparer)
    {
        Name = name;
        _streams = new Dictionary<TStreamId, List<StoredEvent<TEvent, TStreamId>>>(comparer);
    }

    public string Name { get; }

    public static Builder Create(string name) => new(name);

    public ValueTask<EventStoreAppendResult> AppendAsync(TStreamId streamId, long expectedVersion, IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
    {
        if (streamId is null)
            throw new ArgumentNullException(nameof(streamId));
        if (events is null)
            throw new ArgumentNullException(nameof(events));
        if (expectedVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));

        var pending = events.ToArray();
        if (pending.Length == 0)
            throw new ArgumentException("Event stream append requires at least one event.", nameof(events));
        if (pending.Any(static @event => @event is null))
            throw new ArgumentException("Event stream cannot contain null events.", nameof(events));

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var currentVersion = _streams.TryGetValue(streamId, out var stream) ? stream.Count : 0;
            if (currentVersion != expectedVersion)
                return new(EventStoreAppendResult.Conflict(currentVersion, expectedVersion));

            if (stream is null)
            {
                stream = new List<StoredEvent<TEvent, TStreamId>>();
                _streams[streamId] = stream;
            }

            var appended = 0;
            foreach (var @event in pending)
            {
                appended++;
                stream.Add(new StoredEvent<TEvent, TStreamId>(streamId, currentVersion + appended, @event, DateTimeOffset.UtcNow));
            }

            return new(EventStoreAppendResult.Commit(currentVersion + appended, appended));
        }
    }

    public ValueTask<IReadOnlyList<StoredEvent<TEvent, TStreamId>>> ReadStreamAsync(TStreamId streamId, CancellationToken cancellationToken = default)
    {
        if (streamId is null)
            throw new ArgumentNullException(nameof(streamId));

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return new(_streams.TryGetValue(streamId, out var stream)
                ? stream.ToArray()
                : Array.Empty<StoredEvent<TEvent, TStreamId>>());
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private IEqualityComparer<TStreamId>? _comparer;

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Event store name is required.", nameof(name))
                : name;
        }

        public Builder UseComparer(IEqualityComparer<TStreamId> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        public InMemoryEventStore<TEvent, TStreamId> Build() => new(_name, _comparer);
    }
}

public sealed class StoredEvent<TEvent, TStreamId>
    where TStreamId : notnull
{
    public StoredEvent(TStreamId streamId, long version, TEvent @event, DateTimeOffset storedAt)
    {
        StreamId = streamId;
        Version = version;
        Event = @event;
        StoredAt = storedAt;
    }

    public TStreamId StreamId { get; }

    public long Version { get; }

    public TEvent Event { get; }

    public DateTimeOffset StoredAt { get; }
}

public sealed class EventStoreAppendResult
{
    private EventStoreAppendResult(EventStoreAppendStatus status, long version, long expectedVersion, int appendedCount)
    {
        Status = status;
        Version = version;
        ExpectedVersion = expectedVersion;
        AppendedCount = appendedCount;
    }

    public EventStoreAppendStatus Status { get; }

    public long Version { get; }

    public long ExpectedVersion { get; }

    public int AppendedCount { get; }

    public bool Committed => Status == EventStoreAppendStatus.Committed;

    public static EventStoreAppendResult Commit(long version, int appendedCount)
    {
        if (version < 0)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (appendedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(appendedCount));

        return new(EventStoreAppendStatus.Committed, version, version, appendedCount);
    }

    public static EventStoreAppendResult Conflict(long currentVersion, long expectedVersion)
    {
        if (currentVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(currentVersion));
        if (expectedVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));

        return new(EventStoreAppendStatus.Conflict, currentVersion, expectedVersion, 0);
    }
}

public enum EventStoreAppendStatus
{
    Committed,
    Conflict
}
