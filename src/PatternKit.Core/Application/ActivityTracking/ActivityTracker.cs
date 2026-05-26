namespace PatternKit.Application.ActivityTracking;

/// <summary>
/// Tracks active units of work so dependent flows can block while any activity is in flight.
/// </summary>
public sealed class ActivityTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ActivityRecord> _activities = new(StringComparer.Ordinal);
    private readonly Func<DateTimeOffset> _clock;

    private ActivityTracker(string name, Func<DateTimeOffset> clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Activity tracker name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string Name { get; }

    public bool IsBlocked => ActiveCount > 0;

    public int ActiveCount
    {
        get
        {
            lock (_gate)
                return _activities.Count;
        }
    }

    public ActivityLease Track(string activityName)
        => Track(activityName, null);

    public ActivityLease Track(string activityName, string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(activityName))
            throw new ArgumentException("Activity name cannot be null, empty, or whitespace.", nameof(activityName));

        var id = Guid.NewGuid().ToString("N");
        var record = new ActivityRecord(id, activityName, correlationId, _clock());
        lock (_gate)
            _activities.Add(id, record);

        return new ActivityLease(this, record);
    }

    public bool Complete(string activityId)
    {
        if (string.IsNullOrWhiteSpace(activityId))
            throw new ArgumentException("Activity id cannot be null, empty, or whitespace.", nameof(activityId));

        lock (_gate)
            return _activities.Remove(activityId);
    }

    public IReadOnlyList<ActivityRecord> Snapshot()
    {
        lock (_gate)
            return _activities.Values.OrderBy(static activity => activity.StartedAt).ThenBy(static activity => activity.Id, StringComparer.Ordinal).ToArray();
    }

    public ActivityGateState GetGateState()
        => new(Name, IsBlocked, ActiveCount, Snapshot());

    public static Builder Create(string name = "activity-tracker") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;

        internal Builder(string name)
            => _name = name;

        public Builder WithClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public ActivityTracker Build()
            => new(_name, _clock);
    }
}

public sealed class ActivityLease : IDisposable
{
    private readonly ActivityTracker _tracker;
    private int _released;

    internal ActivityLease(ActivityTracker tracker, ActivityRecord activity)
    {
        _tracker = tracker;
        Activity = activity;
    }

    public ActivityRecord Activity { get; }

    public bool IsActive => Volatile.Read(ref _released) == 0;

    public bool Release()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return false;

        return _tracker.Complete(Activity.Id);
    }

    public void Dispose()
        => Release();
}

public sealed class ActivityRecord
{
    public ActivityRecord(string id, string name, string? correlationId, DateTimeOffset startedAt)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Activity id cannot be null, empty, or whitespace.", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Activity name cannot be null, empty, or whitespace.", nameof(name));

        Id = id;
        Name = name;
        CorrelationId = correlationId;
        StartedAt = startedAt;
    }

    public string Id { get; }

    public string Name { get; }

    public string? CorrelationId { get; }

    public DateTimeOffset StartedAt { get; }
}

public sealed class ActivityGateState
{
    public ActivityGateState(string trackerName, bool isBlocked, int activeCount, IReadOnlyList<ActivityRecord> activeActivities)
    {
        TrackerName = trackerName;
        IsBlocked = isBlocked;
        ActiveCount = activeCount;
        ActiveActivities = activeActivities ?? throw new ArgumentNullException(nameof(activeActivities));
    }

    public string TrackerName { get; }

    public bool IsBlocked { get; }

    public int ActiveCount { get; }

    public IReadOnlyList<ActivityRecord> ActiveActivities { get; }
}
