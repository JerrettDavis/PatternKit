namespace PatternKit.Application.EventualConsistency;

public sealed class EventualConsistencyMonitor<TKey>
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, EventualConsistencyWatermarks<TKey>> _streams;
    private readonly Func<DateTimeOffset> _clock;
    private readonly long _maxAllowedLag;

    private EventualConsistencyMonitor(string name, long maxAllowedLag, IEqualityComparer<TKey>? comparer, Func<DateTimeOffset> clock)
    {
        Name = name;
        _maxAllowedLag = maxAllowedLag;
        _streams = new Dictionary<TKey, EventualConsistencyWatermarks<TKey>>(comparer);
        _clock = clock;
    }

    public string Name { get; }

    public long MaxAllowedLag => _maxAllowedLag;

    public int Count
    {
        get
        {
            lock (_gate)
                return _streams.Count;
        }
    }

    public static Builder Create(string name = "eventual-consistency-monitor") => new(name);

    public EventualConsistencyEvaluation<TKey> RecordSource(TKey key, long watermark, string? correlationId = null)
        => RecordCore(key, sourceWatermark: watermark, targetWatermark: null, correlationId);

    public EventualConsistencyEvaluation<TKey> RecordTarget(TKey key, long watermark, string? correlationId = null)
        => RecordCore(key, sourceWatermark: null, targetWatermark: watermark, correlationId);

    public EventualConsistencyEvaluation<TKey> Record(TKey key, long sourceWatermark, long targetWatermark, string? correlationId = null)
    {
        if (sourceWatermark < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWatermark));
        if (targetWatermark < 0)
            throw new ArgumentOutOfRangeException(nameof(targetWatermark));

        return RecordCore(key, sourceWatermark, targetWatermark, correlationId);
    }

    public EventualConsistencyEvaluation<TKey> Evaluate(TKey key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        lock (_gate)
            return _streams.TryGetValue(key, out var watermarks)
                ? Evaluate(watermarks)
                : EventualConsistencyEvaluation<TKey>.Unknown(Name, key, _maxAllowedLag);
    }

    public IReadOnlyList<EventualConsistencyEvaluation<TKey>> Snapshot()
    {
        lock (_gate)
            return _streams.Values.Select(Evaluate).OrderBy(static evaluation => evaluation.Lag).ToArray();
    }

    public EventualConsistencyMonitorState<TKey> GetState()
        => new(Name, _maxAllowedLag, Count, Snapshot());

    private EventualConsistencyEvaluation<TKey> RecordCore(TKey key, long? sourceWatermark, long? targetWatermark, string? correlationId)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (sourceWatermark < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWatermark));
        if (targetWatermark < 0)
            throw new ArgumentOutOfRangeException(nameof(targetWatermark));

        lock (_gate)
        {
            var existing = _streams.TryGetValue(key, out var current)
                ? current
                : new EventualConsistencyWatermarks<TKey>(key, null, null, null, null, null, null);
            var observedAt = _clock();
            var updated = existing.Update(
                sourceWatermark,
                targetWatermark,
                sourceWatermark.HasValue ? observedAt : null,
                targetWatermark.HasValue ? observedAt : null,
                correlationId);
            _streams[key] = updated;
            return Evaluate(updated);
        }
    }

    private EventualConsistencyEvaluation<TKey> Evaluate(EventualConsistencyWatermarks<TKey> watermarks)
    {
        if (!watermarks.SourceWatermark.HasValue)
            return EventualConsistencyEvaluation<TKey>.MissingSource(Name, watermarks, _maxAllowedLag);
        if (!watermarks.TargetWatermark.HasValue)
            return EventualConsistencyEvaluation<TKey>.MissingTarget(Name, watermarks, _maxAllowedLag);

        var lag = Math.Max(0, watermarks.SourceWatermark.Value - watermarks.TargetWatermark.Value);
        return lag <= _maxAllowedLag
            ? EventualConsistencyEvaluation<TKey>.Converged(Name, watermarks, _maxAllowedLag, lag)
            : EventualConsistencyEvaluation<TKey>.Lagging(Name, watermarks, _maxAllowedLag, lag);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private long _maxAllowedLag;
        private IEqualityComparer<TKey>? _comparer;
        private Func<DateTimeOffset> _clock = () => DateTimeOffset.UtcNow;

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Eventual consistency monitor name is required.", nameof(name))
                : name;
        }

        public Builder WithMaxAllowedLag(long maxAllowedLag)
        {
            _maxAllowedLag = maxAllowedLag < 0 ? throw new ArgumentOutOfRangeException(nameof(maxAllowedLag)) : maxAllowedLag;
            return this;
        }

        public Builder UseComparer(IEqualityComparer<TKey> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        public Builder WithClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public EventualConsistencyMonitor<TKey> Build()
            => new(_name, _maxAllowedLag, _comparer, _clock);
    }
}

public sealed class EventualConsistencyWatermarks<TKey>
    where TKey : notnull
{
    public EventualConsistencyWatermarks(
        TKey key,
        long? sourceWatermark,
        long? targetWatermark,
        DateTimeOffset? sourceObservedAt,
        DateTimeOffset? targetObservedAt,
        DateTimeOffset? lastObservedAt,
        string? correlationId)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        SourceWatermark = sourceWatermark;
        TargetWatermark = targetWatermark;
        SourceObservedAt = sourceObservedAt;
        TargetObservedAt = targetObservedAt;
        LastObservedAt = lastObservedAt;
        CorrelationId = correlationId;
    }

    public TKey Key { get; }

    public long? SourceWatermark { get; }

    public long? TargetWatermark { get; }

    public DateTimeOffset? SourceObservedAt { get; }

    public DateTimeOffset? TargetObservedAt { get; }

    public DateTimeOffset? LastObservedAt { get; }

    public string? CorrelationId { get; }

    public EventualConsistencyWatermarks<TKey> Update(
        long? sourceWatermark,
        long? targetWatermark,
        DateTimeOffset? sourceObservedAt,
        DateTimeOffset? targetObservedAt,
        string? correlationId)
        => new(
            Key,
            sourceWatermark ?? SourceWatermark,
            targetWatermark ?? TargetWatermark,
            sourceObservedAt ?? SourceObservedAt,
            targetObservedAt ?? TargetObservedAt,
            sourceObservedAt ?? targetObservedAt ?? LastObservedAt,
            correlationId ?? CorrelationId);
}

public sealed class EventualConsistencyEvaluation<TKey>
    where TKey : notnull
{
    private EventualConsistencyEvaluation(
        string monitorName,
        TKey key,
        EventualConsistencyStatus status,
        long maxAllowedLag,
        long lag,
        EventualConsistencyWatermarks<TKey>? watermarks)
    {
        MonitorName = monitorName;
        Key = key;
        Status = status;
        MaxAllowedLag = maxAllowedLag;
        Lag = lag;
        Watermarks = watermarks;
    }

    public string MonitorName { get; }

    public TKey Key { get; }

    public EventualConsistencyStatus Status { get; }

    public long MaxAllowedLag { get; }

    public long Lag { get; }

    public EventualConsistencyWatermarks<TKey>? Watermarks { get; }

    public bool IsConverged => Status == EventualConsistencyStatus.Converged;

    public static EventualConsistencyEvaluation<TKey> Unknown(string monitorName, TKey key, long maxAllowedLag)
        => new(monitorName, key, EventualConsistencyStatus.Unknown, maxAllowedLag, 0, null);

    public static EventualConsistencyEvaluation<TKey> MissingSource(string monitorName, EventualConsistencyWatermarks<TKey> watermarks, long maxAllowedLag)
        => new(monitorName, watermarks.Key, EventualConsistencyStatus.MissingSource, maxAllowedLag, 0, watermarks);

    public static EventualConsistencyEvaluation<TKey> MissingTarget(string monitorName, EventualConsistencyWatermarks<TKey> watermarks, long maxAllowedLag)
        => new(monitorName, watermarks.Key, EventualConsistencyStatus.MissingTarget, maxAllowedLag, 0, watermarks);

    public static EventualConsistencyEvaluation<TKey> Converged(string monitorName, EventualConsistencyWatermarks<TKey> watermarks, long maxAllowedLag, long lag)
        => new(monitorName, watermarks.Key, EventualConsistencyStatus.Converged, maxAllowedLag, lag, watermarks);

    public static EventualConsistencyEvaluation<TKey> Lagging(string monitorName, EventualConsistencyWatermarks<TKey> watermarks, long maxAllowedLag, long lag)
        => new(monitorName, watermarks.Key, EventualConsistencyStatus.Lagging, maxAllowedLag, lag, watermarks);
}

public sealed class EventualConsistencyMonitorState<TKey>
    where TKey : notnull
{
    public EventualConsistencyMonitorState(
        string name,
        long maxAllowedLag,
        int count,
        IReadOnlyList<EventualConsistencyEvaluation<TKey>> evaluations)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Eventual consistency monitor name is required.", nameof(name))
            : name;
        MaxAllowedLag = maxAllowedLag < 0 ? throw new ArgumentOutOfRangeException(nameof(maxAllowedLag)) : maxAllowedLag;
        Count = count < 0 ? throw new ArgumentOutOfRangeException(nameof(count)) : count;
        Evaluations = evaluations ?? throw new ArgumentNullException(nameof(evaluations));
    }

    public string Name { get; }

    public long MaxAllowedLag { get; }

    public int Count { get; }

    public IReadOnlyList<EventualConsistencyEvaluation<TKey>> Evaluations { get; }
}

public enum EventualConsistencyStatus
{
    Unknown,
    MissingSource,
    MissingTarget,
    Lagging,
    Converged
}
