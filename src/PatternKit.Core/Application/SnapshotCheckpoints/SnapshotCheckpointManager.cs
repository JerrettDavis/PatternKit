namespace PatternKit.Application.SnapshotCheckpoints;

public sealed class SnapshotCheckpointManager<TKey, TSnapshot>
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, SnapshotCheckpoint<TKey, TSnapshot>> _checkpoints;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SnapshotCheckpointStaleWritePolicy _staleWritePolicy;

    private SnapshotCheckpointManager(
        string name,
        IEqualityComparer<TKey>? comparer,
        Func<DateTimeOffset> clock,
        SnapshotCheckpointStaleWritePolicy staleWritePolicy)
    {
        Name = name;
        _checkpoints = new Dictionary<TKey, SnapshotCheckpoint<TKey, TSnapshot>>(comparer);
        _clock = clock;
        _staleWritePolicy = staleWritePolicy;
    }

    public string Name { get; }

    public int Count
    {
        get
        {
            lock (_gate)
                return _checkpoints.Count;
        }
    }

    public static Builder Create(string name = "snapshot-checkpoints") => new(name);

    public SnapshotCheckpointSaveResult<TKey, TSnapshot> Save(
        TKey key,
        long version,
        TSnapshot snapshot,
        string? correlationId = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (version < 0)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        lock (_gate)
        {
            if (_checkpoints.TryGetValue(key, out var existing)
                && version < existing.Version
                && _staleWritePolicy == SnapshotCheckpointStaleWritePolicy.Reject)
                return SnapshotCheckpointSaveResult<TKey, TSnapshot>.RejectedStale(existing, version);

            var checkpoint = new SnapshotCheckpoint<TKey, TSnapshot>(
                key,
                version,
                snapshot,
                _clock(),
                correlationId,
                metadata ?? new Dictionary<string, string>());
            _checkpoints[key] = checkpoint;
            return SnapshotCheckpointSaveResult<TKey, TSnapshot>.Saved(checkpoint, existing);
        }
    }

    public SnapshotCheckpointLoadResult<TKey, TSnapshot> Load(TKey key, long minimumVersion = 0)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (minimumVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumVersion));

        lock (_gate)
        {
            if (!_checkpoints.TryGetValue(key, out var checkpoint))
                return SnapshotCheckpointLoadResult<TKey, TSnapshot>.Missing(key, minimumVersion);

            return checkpoint.Version < minimumVersion
                ? SnapshotCheckpointLoadResult<TKey, TSnapshot>.Stale(checkpoint, minimumVersion)
                : SnapshotCheckpointLoadResult<TKey, TSnapshot>.Found(checkpoint, minimumVersion);
        }
    }

    public bool Remove(TKey key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        lock (_gate)
            return _checkpoints.Remove(key);
    }

    public IReadOnlyList<SnapshotCheckpoint<TKey, TSnapshot>> Snapshot()
    {
        lock (_gate)
            return _checkpoints.Values.OrderBy(static checkpoint => checkpoint.Version).ToArray();
    }

    public SnapshotCheckpointManagerState<TKey, TSnapshot> GetState()
        => new(Name, Count, Snapshot());

    public sealed class Builder
    {
        private readonly string _name;
        private IEqualityComparer<TKey>? _comparer;
        private Func<DateTimeOffset> _clock = () => DateTimeOffset.UtcNow;
        private SnapshotCheckpointStaleWritePolicy _staleWritePolicy = SnapshotCheckpointStaleWritePolicy.Reject;

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Snapshot checkpoint manager name is required.", nameof(name))
                : name;
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

        public Builder WithStaleWritePolicy(SnapshotCheckpointStaleWritePolicy policy)
        {
            if (policy != SnapshotCheckpointStaleWritePolicy.Reject && policy != SnapshotCheckpointStaleWritePolicy.Overwrite)
                throw new ArgumentOutOfRangeException(nameof(policy));

            _staleWritePolicy = policy;
            return this;
        }

        public SnapshotCheckpointManager<TKey, TSnapshot> Build()
            => new(_name, _comparer, _clock, _staleWritePolicy);
    }
}

public sealed class SnapshotCheckpoint<TKey, TSnapshot>
    where TKey : notnull
{
    public SnapshotCheckpoint(
        TKey key,
        long version,
        TSnapshot snapshot,
        DateTimeOffset savedAt,
        string? correlationId,
        IReadOnlyDictionary<string, string> metadata)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (version < 0)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));

        Key = key;
        Version = version;
        Snapshot = snapshot;
        SavedAt = savedAt;
        CorrelationId = correlationId;
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public TKey Key { get; }

    public long Version { get; }

    public TSnapshot Snapshot { get; }

    public DateTimeOffset SavedAt { get; }

    public string? CorrelationId { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public sealed class SnapshotCheckpointSaveResult<TKey, TSnapshot>
    where TKey : notnull
{
    private SnapshotCheckpointSaveResult(
        SnapshotCheckpointSaveStatus status,
        SnapshotCheckpoint<TKey, TSnapshot>? checkpoint,
        SnapshotCheckpoint<TKey, TSnapshot>? previousCheckpoint,
        long attemptedVersion)
    {
        Status = status;
        Checkpoint = checkpoint;
        PreviousCheckpoint = previousCheckpoint;
        AttemptedVersion = attemptedVersion;
    }

    public SnapshotCheckpointSaveStatus Status { get; }

    public SnapshotCheckpoint<TKey, TSnapshot>? Checkpoint { get; }

    public SnapshotCheckpoint<TKey, TSnapshot>? PreviousCheckpoint { get; }

    public long AttemptedVersion { get; }

    public bool IsSaved => Status == SnapshotCheckpointSaveStatus.Saved;

    public static SnapshotCheckpointSaveResult<TKey, TSnapshot> Saved(
        SnapshotCheckpoint<TKey, TSnapshot> checkpoint,
        SnapshotCheckpoint<TKey, TSnapshot>? previousCheckpoint)
        => new(SnapshotCheckpointSaveStatus.Saved, checkpoint, previousCheckpoint, checkpoint.Version);

    public static SnapshotCheckpointSaveResult<TKey, TSnapshot> RejectedStale(
        SnapshotCheckpoint<TKey, TSnapshot> existing,
        long attemptedVersion)
        => new(SnapshotCheckpointSaveStatus.RejectedStale, existing, existing, attemptedVersion);
}

public sealed class SnapshotCheckpointLoadResult<TKey, TSnapshot>
    where TKey : notnull
{
    private SnapshotCheckpointLoadResult(
        SnapshotCheckpointLoadStatus status,
        TKey key,
        SnapshotCheckpoint<TKey, TSnapshot>? checkpoint,
        long minimumVersion)
    {
        Key = key;
        Status = status;
        Checkpoint = checkpoint;
        MinimumVersion = minimumVersion;
    }

    public TKey Key { get; }

    public SnapshotCheckpointLoadStatus Status { get; }

    public SnapshotCheckpoint<TKey, TSnapshot>? Checkpoint { get; }

    public long MinimumVersion { get; }

    public bool IsUsable => Status == SnapshotCheckpointLoadStatus.Found;

    public static SnapshotCheckpointLoadResult<TKey, TSnapshot> Found(
        SnapshotCheckpoint<TKey, TSnapshot> checkpoint,
        long minimumVersion)
        => new(SnapshotCheckpointLoadStatus.Found, checkpoint.Key, checkpoint, minimumVersion);

    public static SnapshotCheckpointLoadResult<TKey, TSnapshot> Missing(TKey key, long minimumVersion)
        => new(SnapshotCheckpointLoadStatus.Missing, key, null, minimumVersion);

    public static SnapshotCheckpointLoadResult<TKey, TSnapshot> Stale(
        SnapshotCheckpoint<TKey, TSnapshot> checkpoint,
        long minimumVersion)
        => new(SnapshotCheckpointLoadStatus.Stale, checkpoint.Key, checkpoint, minimumVersion);
}

public sealed class SnapshotCheckpointManagerState<TKey, TSnapshot>
    where TKey : notnull
{
    public SnapshotCheckpointManagerState(
        string name,
        int count,
        IReadOnlyList<SnapshotCheckpoint<TKey, TSnapshot>> checkpoints)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Snapshot checkpoint manager name is required.", nameof(name))
            : name;
        Count = count < 0 ? throw new ArgumentOutOfRangeException(nameof(count)) : count;
        Checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
    }

    public string Name { get; }

    public int Count { get; }

    public IReadOnlyList<SnapshotCheckpoint<TKey, TSnapshot>> Checkpoints { get; }
}

public enum SnapshotCheckpointLoadStatus
{
    Missing,
    Found,
    Stale
}

public enum SnapshotCheckpointSaveStatus
{
    Saved,
    RejectedStale
}

public enum SnapshotCheckpointStaleWritePolicy
{
    Reject,
    Overwrite
}
