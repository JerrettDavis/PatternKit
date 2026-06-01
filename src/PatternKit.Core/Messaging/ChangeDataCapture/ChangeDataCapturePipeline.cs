namespace PatternKit.Messaging.ChangeDataCapture;

public enum ChangeDataCaptureOperation
{
    Insert,
    Update,
    Delete,
    Upsert
}

public sealed class ChangeDataCaptureMutation<TKey, TPayload>
{
    public ChangeDataCaptureMutation(TKey key, ChangeDataCaptureOperation operation, string entityName, TPayload payload, long version, DateTimeOffset occurredAt)
    {
        Key = key;
        Operation = operation;
        EntityName = string.IsNullOrWhiteSpace(entityName) ? throw new ArgumentException("Entity name is required.", nameof(entityName)) : entityName;
        Payload = payload;
        Version = version;
        OccurredAt = occurredAt;
    }

    public TKey Key { get; }
    public ChangeDataCaptureOperation Operation { get; }
    public string EntityName { get; }
    public TPayload Payload { get; }
    public long Version { get; }
    public DateTimeOffset OccurredAt { get; }
}

public sealed class ChangeDataCaptureEntry<TMutation, TEvent>
{
    public ChangeDataCaptureEntry(
        long sequence,
        string pipelineName,
        TMutation mutation,
        TEvent @event,
        DateTimeOffset capturedAt,
        bool published = false,
        DateTimeOffset? publishedAt = null,
        int attempts = 0)
    {
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "CDC sequence must be positive.");

        Sequence = sequence;
        PipelineName = string.IsNullOrWhiteSpace(pipelineName) ? throw new ArgumentException("CDC pipeline name is required.", nameof(pipelineName)) : pipelineName;
        Mutation = mutation;
        Event = @event;
        CapturedAt = capturedAt;
        Published = published;
        PublishedAt = publishedAt;
        Attempts = attempts;
    }

    public long Sequence { get; }
    public string PipelineName { get; }
    public TMutation Mutation { get; }
    public TEvent Event { get; }
    public DateTimeOffset CapturedAt { get; }
    public bool Published { get; }
    public DateTimeOffset? PublishedAt { get; }
    public int Attempts { get; }

    public ChangeDataCaptureEntry<TMutation, TEvent> MarkPublished(DateTimeOffset publishedAt)
        => new(Sequence, PipelineName, Mutation, Event, CapturedAt, true, publishedAt, Attempts + 1);

    public ChangeDataCaptureEntry<TMutation, TEvent> MarkAttempt()
        => new(Sequence, PipelineName, Mutation, Event, CapturedAt, Published, PublishedAt, Attempts + 1);
}

public readonly struct ChangeDataCapturePublishSummary : IEquatable<ChangeDataCapturePublishSummary>
{
    public ChangeDataCapturePublishSummary(int published, int failed)
    {
        Published = published;
        Failed = failed;
    }

    public int Published { get; }
    public int Failed { get; }

    public bool Equals(ChangeDataCapturePublishSummary other)
        => Published == other.Published && Failed == other.Failed;

    public override bool Equals(object? obj)
        => obj is ChangeDataCapturePublishSummary other && Equals(other);

    public override int GetHashCode()
        => (Published * 397) ^ Failed;

    public static bool operator ==(ChangeDataCapturePublishSummary left, ChangeDataCapturePublishSummary right)
        => left.Equals(right);

    public static bool operator !=(ChangeDataCapturePublishSummary left, ChangeDataCapturePublishSummary right)
        => !left.Equals(right);
}

public interface IChangeDataCaptureStore<TMutation, TEvent>
{
    ValueTask<long> GetNextSequenceAsync(string pipelineName, CancellationToken cancellationToken = default);

    ValueTask<ChangeDataCaptureEntry<TMutation, TEvent>> AppendAsync(
        string pipelineName,
        long sequence,
        TMutation mutation,
        TEvent @event,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ChangeDataCaptureEntry<TMutation, TEvent>>> ReadPendingAsync(
        string pipelineName,
        CancellationToken cancellationToken = default);

    ValueTask MarkPublishedAsync(
        long sequence,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken = default);

    ValueTask MarkAttemptAsync(
        long sequence,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryChangeDataCaptureStore<TMutation, TEvent> : IChangeDataCaptureStore<TMutation, TEvent>
{
    private readonly object _sync = new();
    private readonly List<ChangeDataCaptureEntry<TMutation, TEvent>> _entries = [];
    private long _sequence;

    public ValueTask<long> GetNextSequenceAsync(string pipelineName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(pipelineName))
            throw new ArgumentException("CDC pipeline name is required.", nameof(pipelineName));

        lock (_sync)
            return new(_sequence + 1);
    }

    public ValueTask<ChangeDataCaptureEntry<TMutation, TEvent>> AppendAsync(
        string pipelineName,
        long sequence,
        TMutation mutation,
        TEvent @event,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(pipelineName))
            throw new ArgumentException("CDC pipeline name is required.", nameof(pipelineName));
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "CDC sequence must be positive.");

        lock (_sync)
        {
            if (sequence <= _sequence)
                throw new InvalidOperationException($"CDC sequence '{sequence}' has already been used.");

            _sequence = sequence;
            var entry = new ChangeDataCaptureEntry<TMutation, TEvent>(sequence, pipelineName, mutation, @event, capturedAt);
            _entries.Add(entry);
            return new(entry);
        }
    }

    public ValueTask<IReadOnlyList<ChangeDataCaptureEntry<TMutation, TEvent>>> ReadPendingAsync(
        string pipelineName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(pipelineName))
            throw new ArgumentException("CDC pipeline name is required.", nameof(pipelineName));

        ChangeDataCaptureEntry<TMutation, TEvent>[] pending;
        lock (_sync)
        {
            pending = _entries
                .Where(entry => string.Equals(entry.PipelineName, pipelineName, StringComparison.Ordinal) && !entry.Published)
                .OrderBy(static entry => entry.Sequence)
                .ToArray();
        }

        return new(pending);
    }

    public ValueTask MarkPublishedAsync(
        long sequence,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(sequence, entry => entry.MarkPublished(publishedAt));
        return default;
    }

    public ValueTask MarkAttemptAsync(long sequence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Update(sequence, static entry => entry.MarkAttempt());
        return default;
    }

    public IReadOnlyList<ChangeDataCaptureEntry<TMutation, TEvent>> Snapshot()
    {
        lock (_sync)
            return _entries.ToArray();
    }

    private void Update(long sequence, Func<ChangeDataCaptureEntry<TMutation, TEvent>, ChangeDataCaptureEntry<TMutation, TEvent>> update)
    {
        lock (_sync)
        {
            var index = _entries.FindIndex(entry => entry.Sequence == sequence);
            if (index < 0)
                throw new KeyNotFoundException($"CDC entry '{sequence}' was not found.");

            _entries[index] = update(_entries[index]);
        }
    }
}

public sealed class ChangeDataCapturePipeline<TMutation, TEvent>
{
    private readonly Func<TMutation, long, TEvent> _eventFactory;
    private readonly Func<TEvent, CancellationToken, ValueTask> _publisher;
    private readonly IChangeDataCaptureStore<TMutation, TEvent> _store;
    private readonly Func<DateTimeOffset> _utcNow;

    private ChangeDataCapturePipeline(
        string name,
        Func<TMutation, long, TEvent> eventFactory,
        Func<TEvent, CancellationToken, ValueTask> publisher,
        IChangeDataCaptureStore<TMutation, TEvent> store,
        Func<DateTimeOffset> utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("CDC pipeline name is required.", nameof(name));

        Name = name;
        _eventFactory = eventFactory ?? throw new ArgumentNullException(nameof(eventFactory));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public string Name { get; }

    public static Builder Create(string name = "change-data-capture") => new(name);

    public async ValueTask<ChangeDataCaptureEntry<TMutation, TEvent>> CaptureAsync(
        TMutation mutation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mutation is null)
            throw new ArgumentNullException(nameof(mutation));

        var nextSequence = await _store.GetNextSequenceAsync(Name, cancellationToken).ConfigureAwait(false);
        var @event = _eventFactory(mutation, nextSequence);
        return await _store.AppendAsync(Name, nextSequence, mutation, @event, _utcNow(), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ChangeDataCapturePublishSummary> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var published = 0;
        var failed = 0;

        foreach (var entry in await _store.ReadPendingAsync(Name, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await _publisher(entry.Event, cancellationToken).ConfigureAwait(false);
                await _store.MarkPublishedAsync(entry.Sequence, _utcNow(), cancellationToken).ConfigureAwait(false);
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                await _store.MarkAttemptAsync(entry.Sequence, cancellationToken).ConfigureAwait(false);
                failed++;
            }
        }

        return new(published, failed);
    }

    public ValueTask<IReadOnlyList<ChangeDataCaptureEntry<TMutation, TEvent>>> ReadPendingAsync(CancellationToken cancellationToken = default)
        => _store.ReadPendingAsync(Name, cancellationToken);

    public sealed class Builder
    {
        private readonly string _name;
        private Func<TMutation, long, TEvent>? _eventFactory;
        private Func<TEvent, CancellationToken, ValueTask>? _publisher;
        private IChangeDataCaptureStore<TMutation, TEvent> _store = new InMemoryChangeDataCaptureStore<TMutation, TEvent>();
        private Func<DateTimeOffset> _utcNow = () => DateTimeOffset.UtcNow;

        internal Builder(string name) => _name = name;

        public Builder MapWith(Func<TMutation, long, TEvent> eventFactory)
        {
            _eventFactory = eventFactory ?? throw new ArgumentNullException(nameof(eventFactory));
            return this;
        }

        public Builder PublishWith(Func<TEvent, CancellationToken, ValueTask> publisher)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            return this;
        }

        public Builder UseStore(IChangeDataCaptureStore<TMutation, TEvent> store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        public Builder WithClock(Func<DateTimeOffset> utcNow)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            return this;
        }

        public ChangeDataCapturePipeline<TMutation, TEvent> Build()
        {
            if (_eventFactory is null)
                throw new InvalidOperationException("CDC pipeline requires an event mapper.");
            if (_publisher is null)
                throw new InvalidOperationException("CDC pipeline requires a publisher.");

            return new(_name, _eventFactory, _publisher, _store, _utcNow);
        }
    }
}
