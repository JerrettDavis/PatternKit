namespace PatternKit.Application.Timeouts;

/// <summary>
/// Coordinates time-based deadlines for workflows that must expire, complete, or cancel pending work.
/// </summary>
public sealed class TimeoutManager<TKey>
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, TimeoutRecord<TKey>> _timeouts;
    private readonly Func<DateTimeOffset> _clock;

    private TimeoutManager(string name, IEqualityComparer<TKey> keyComparer, Func<DateTimeOffset> clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Timeout manager name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeouts = new Dictionary<TKey, TimeoutRecord<TKey>>(keyComparer ?? throw new ArgumentNullException(nameof(keyComparer)));
    }

    public string Name { get; }

    public int PendingCount
    {
        get
        {
            lock (_gate)
                return _timeouts.Count;
        }
    }

    public TimeoutRecord<TKey> Schedule(TKey key, DateTimeOffset deadline, string? correlationId = null)
    {
        var now = _clock();
        return ScheduleCore(key, correlationId, now, deadline);
    }

    public TimeoutRecord<TKey> ScheduleAfter(TKey key, TimeSpan dueAfter, string? correlationId = null)
    {
        if (dueAfter < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(dueAfter), dueAfter, "Timeout duration cannot be negative.");

        var now = _clock();
        return ScheduleCore(key, correlationId, now, now.Add(dueAfter));
    }

    private TimeoutRecord<TKey> ScheduleCore(TKey key, string? correlationId, DateTimeOffset now, DateTimeOffset deadline)
    {
        if (deadline < now)
            throw new ArgumentOutOfRangeException(nameof(deadline), deadline, "Timeout deadline cannot be before the current clock value.");

        var record = new TimeoutRecord<TKey>(key, correlationId, now, deadline);
        lock (_gate)
            _timeouts[key] = record;

        return record;
    }

    public bool Complete(TKey key) => Remove(key);

    public bool Cancel(TKey key) => Remove(key);

    public IReadOnlyList<TimeoutRecord<TKey>> ExpireDue()
        => ExpireDue(_clock());

    public IReadOnlyList<TimeoutRecord<TKey>> ExpireDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            var due = _timeouts.Values
                .Where(timeout => timeout.Deadline <= now)
                .OrderBy(static timeout => timeout.Deadline)
                .ThenBy(static timeout => timeout.CorrelationId, StringComparer.Ordinal)
                .ToArray();

            foreach (var timeout in due)
                _timeouts.Remove(timeout.Key);

            return due;
        }
    }

    public IReadOnlyList<TimeoutRecord<TKey>> Snapshot()
    {
        lock (_gate)
            return _timeouts.Values
                .OrderBy(static timeout => timeout.Deadline)
                .ThenBy(static timeout => timeout.CorrelationId, StringComparer.Ordinal)
                .ToArray();
    }

    public TimeoutManagerState<TKey> GetState()
    {
        lock (_gate)
        {
            var pendingTimeouts = _timeouts.Values
                .OrderBy(static timeout => timeout.Deadline)
                .ThenBy(static timeout => timeout.CorrelationId, StringComparer.Ordinal)
                .ToArray();

            return new(Name, pendingTimeouts.Length, pendingTimeouts);
        }
    }

    public static Builder Create(string name = "timeout-manager") => new(name);

    private bool Remove(TKey key)
    {
        lock (_gate)
            return _timeouts.Remove(key);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private IEqualityComparer<TKey> _keyComparer = EqualityComparer<TKey>.Default;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;

        internal Builder(string name)
            => _name = name;

        public Builder WithKeyComparer(IEqualityComparer<TKey> keyComparer)
        {
            _keyComparer = keyComparer ?? throw new ArgumentNullException(nameof(keyComparer));
            return this;
        }

        public Builder WithClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public TimeoutManager<TKey> Build()
            => new(_name, _keyComparer, _clock);
    }
}

public sealed class TimeoutRecord<TKey>
    where TKey : notnull
{
    public TimeoutRecord(TKey key, string? correlationId, DateTimeOffset scheduledAt, DateTimeOffset deadline)
    {
        Key = key;
        CorrelationId = correlationId;
        ScheduledAt = scheduledAt;
        Deadline = deadline;
    }

    public TKey Key { get; }

    public string? CorrelationId { get; }

    public DateTimeOffset ScheduledAt { get; }

    public DateTimeOffset Deadline { get; }

    public bool IsDue(DateTimeOffset now) => Deadline <= now;
}

public sealed class TimeoutManagerState<TKey>
    where TKey : notnull
{
    public TimeoutManagerState(string managerName, int pendingCount, IReadOnlyList<TimeoutRecord<TKey>> pendingTimeouts)
    {
        ManagerName = managerName;
        PendingCount = pendingCount;
        PendingTimeouts = pendingTimeouts ?? throw new ArgumentNullException(nameof(pendingTimeouts));
    }

    public string ManagerName { get; }

    public int PendingCount { get; }

    public IReadOnlyList<TimeoutRecord<TKey>> PendingTimeouts { get; }
}
