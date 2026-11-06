namespace PatternKit.Application.ManualTaskGates;

/// <summary>
/// Tracks human-owned tasks that block a workflow until they are approved, rejected, canceled, or completed.
/// </summary>
public sealed class ManualTaskGate<TKey>
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, ManualTaskRecord<TKey>> _tasks;
    private readonly Func<DateTimeOffset> _clock;

    private ManualTaskGate(string name, IEqualityComparer<TKey> keyComparer, Func<DateTimeOffset> clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Manual task gate name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _tasks = new Dictionary<TKey, ManualTaskRecord<TKey>>(keyComparer ?? throw new ArgumentNullException(nameof(keyComparer)));
    }

    public string Name { get; }

    public bool IsBlocked => PendingCount > 0;

    public int PendingCount
    {
        get
        {
            lock (_gate)
                return _tasks.Values.Count(static task => task.IsBlocking);
        }
    }

    public ManualTaskRecord<TKey> Open(TKey key, string taskName, string? assignee = null, string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Manual task name cannot be null, empty, or whitespace.", nameof(taskName));

        var record = new ManualTaskRecord<TKey>(
            key,
            taskName,
            assignee,
            correlationId,
            ManualTaskStatus.Pending,
            _clock(),
            null,
            null,
            null);

        lock (_gate)
            _tasks[key] = record;

        return record;
    }

    public ManualTaskRecord<TKey>? Approve(TKey key, string actor, string? decisionNote = null)
        => Decide(key, ManualTaskStatus.Approved, actor, decisionNote);

    public ManualTaskRecord<TKey>? Reject(TKey key, string actor, string? decisionNote = null)
        => Decide(key, ManualTaskStatus.Rejected, actor, decisionNote);

    public ManualTaskRecord<TKey>? Cancel(TKey key, string actor, string? decisionNote = null)
        => Decide(key, ManualTaskStatus.Canceled, actor, decisionNote);

    public bool Complete(TKey key)
    {
        lock (_gate)
            return _tasks.Remove(key);
    }

    public IReadOnlyList<ManualTaskRecord<TKey>> Snapshot()
    {
        lock (_gate)
            return _tasks.Values
                .OrderBy(static task => task.OpenedAt)
                .ThenBy(static task => task.TaskName, StringComparer.Ordinal)
                .ToArray();
    }

    public ManualTaskGateState<TKey> GetGateState()
    {
        lock (_gate)
        {
            var tasks = _tasks.Values
                .OrderBy(static task => task.OpenedAt)
                .ThenBy(static task => task.TaskName, StringComparer.Ordinal)
                .ToArray();
            var pending = tasks.Where(static task => task.IsBlocking).ToArray();

            return new(Name, pending.Length > 0, pending.Length, pending, tasks);
        }
    }

    public static Builder Create(string name = "manual-task-gate") => new(name);

    private ManualTaskRecord<TKey>? Decide(TKey key, ManualTaskStatus status, string actor, string? decisionNote)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("Manual task decision actor cannot be null, empty, or whitespace.", nameof(actor));

        lock (_gate)
        {
            if (!_tasks.TryGetValue(key, out var current))
                return null;

            if (!current.IsBlocking)
                return current;

            var decided = current.WithDecision(status, actor, decisionNote, _clock());
            _tasks[key] = decided;
            return decided;
        }
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

        public ManualTaskGate<TKey> Build()
            => new(_name, _keyComparer, _clock);
    }
}

public enum ManualTaskStatus
{
    Pending,
    Approved,
    Rejected,
    Canceled
}

public sealed class ManualTaskRecord<TKey>
    where TKey : notnull
{
    public ManualTaskRecord(
        TKey key,
        string taskName,
        string? assignee,
        string? correlationId,
        ManualTaskStatus status,
        DateTimeOffset openedAt,
        DateTimeOffset? decidedAt,
        string? decidedBy,
        string? decisionNote)
    {
        if (string.IsNullOrWhiteSpace(taskName))
            throw new ArgumentException("Manual task name cannot be null, empty, or whitespace.", nameof(taskName));

        Key = key;
        TaskName = taskName;
        Assignee = assignee;
        CorrelationId = correlationId;
        Status = status;
        OpenedAt = openedAt;
        DecidedAt = decidedAt;
        DecidedBy = decidedBy;
        DecisionNote = decisionNote;
    }

    public TKey Key { get; }

    public string TaskName { get; }

    public string? Assignee { get; }

    public string? CorrelationId { get; }

    public ManualTaskStatus Status { get; }

    public DateTimeOffset OpenedAt { get; }

    public DateTimeOffset? DecidedAt { get; }

    public string? DecidedBy { get; }

    public string? DecisionNote { get; }

    public bool IsBlocking => Status == ManualTaskStatus.Pending;

    internal ManualTaskRecord<TKey> WithDecision(ManualTaskStatus status, string actor, string? decisionNote, DateTimeOffset decidedAt)
        => new(Key, TaskName, Assignee, CorrelationId, status, OpenedAt, decidedAt, actor, decisionNote);
}

public sealed class ManualTaskGateState<TKey>
    where TKey : notnull
{
    public ManualTaskGateState(
        string gateName,
        bool isBlocked,
        int pendingCount,
        IReadOnlyList<ManualTaskRecord<TKey>> pendingTasks,
        IReadOnlyList<ManualTaskRecord<TKey>> allTasks)
    {
        GateName = gateName;
        IsBlocked = isBlocked;
        PendingCount = pendingCount;
        PendingTasks = pendingTasks ?? throw new ArgumentNullException(nameof(pendingTasks));
        AllTasks = allTasks ?? throw new ArgumentNullException(nameof(allTasks));
    }

    public string GateName { get; }

    public bool IsBlocked { get; }

    public int PendingCount { get; }

    public IReadOnlyList<ManualTaskRecord<TKey>> PendingTasks { get; }

    public IReadOnlyList<ManualTaskRecord<TKey>> AllTasks { get; }
}
