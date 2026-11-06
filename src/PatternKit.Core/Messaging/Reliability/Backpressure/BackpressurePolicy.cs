namespace PatternKit.Messaging.Reliability.Backpressure;

/// <summary>
/// Admission behavior used when a backpressure policy is saturated.
/// </summary>
public enum BackpressureMode
{
    Reject,
    Wait,
    DropNewest,
    DropOldest,
    Shed,
    Observe
}

/// <summary>
/// Outcome returned by a backpressure policy execution.
/// </summary>
public sealed class BackpressureResult<TResult>
{
    public BackpressureResult(
        TResult? value,
        bool accepted,
        bool rejected,
        bool dropped,
        bool shed,
        bool observed,
        bool waited,
        int activeCount,
        int droppedCount)
    {
        Value = value;
        Accepted = accepted;
        Rejected = rejected;
        Dropped = dropped;
        Shed = shed;
        Observed = observed;
        Waited = waited;
        ActiveCount = activeCount;
        DroppedCount = droppedCount;
    }

    public TResult? Value { get; }
    public bool Accepted { get; }
    public bool Rejected { get; }
    public bool Dropped { get; }
    public bool Shed { get; }
    public bool Observed { get; }
    public bool Waited { get; }
    public int ActiveCount { get; }
    public int DroppedCount { get; }

    public static BackpressureResult<TResult> AcceptedResult(TResult value, bool waited, int activeCount, int droppedCount)
        => new(value, true, false, false, false, false, waited, activeCount, droppedCount);

    public static BackpressureResult<TResult> ObservedResult(TResult value, int activeCount, int droppedCount)
        => new(value, true, false, false, false, true, false, activeCount, droppedCount);

    public static BackpressureResult<TResult> RejectedResult(int activeCount, int droppedCount)
        => new(default, false, true, false, false, false, false, activeCount, droppedCount);

    public static BackpressureResult<TResult> DroppedResult(int activeCount, int droppedCount)
        => new(default, false, false, true, false, false, false, activeCount, droppedCount);

    public static BackpressureResult<TResult> ShedResult(int activeCount, int droppedCount)
        => new(default, false, false, false, true, false, false, activeCount, droppedCount);
}

/// <summary>
/// Reusable backpressure gate for bounding active work and applying explicit saturation policies.
/// </summary>
public sealed class BackpressurePolicy<TResult>
{
    private readonly SemaphoreSlim _slots;
    private int _activeCount;
    private int _droppedCount;

    private BackpressurePolicy(string name, int capacity, BackpressureMode mode, TimeSpan waitTimeout)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Backpressure policy name is required.", nameof(name));
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Backpressure capacity must be at least one.");
        if (!IsDefinedMode(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Backpressure mode is not valid.");
        if (waitTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(waitTimeout), waitTimeout, "Backpressure wait timeout cannot be negative.");

        Name = name;
        Capacity = capacity;
        Mode = mode;
        WaitTimeout = waitTimeout;
        _slots = new SemaphoreSlim(capacity, capacity);
    }

    public string Name { get; }
    public int Capacity { get; }
    public BackpressureMode Mode { get; }
    public TimeSpan WaitTimeout { get; }
    public int ActiveCount => Volatile.Read(ref _activeCount);
    public int DroppedCount => Volatile.Read(ref _droppedCount);

    public static Builder Create(string name = "backpressure") => new(name);

    public BackpressureResult<TResult> Execute(Func<TResult> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var lease = Enter();
        if (!lease.Accepted)
            return lease.Outcome switch
            {
                BackpressureOutcome.Dropped => BackpressureResult<TResult>.DroppedResult(ActiveCount, DroppedCount),
                BackpressureOutcome.Shed => BackpressureResult<TResult>.ShedResult(ActiveCount, DroppedCount),
                _ => BackpressureResult<TResult>.RejectedResult(ActiveCount, DroppedCount)
            };

        try
        {
            var value = operation();
            return lease.Observed
                ? BackpressureResult<TResult>.ObservedResult(value, ActiveCount, DroppedCount)
                : BackpressureResult<TResult>.AcceptedResult(value, lease.Waited, ActiveCount, DroppedCount);
        }
        finally
        {
            if (!lease.Observed)
                Exit();
        }
    }

    public async ValueTask<BackpressureResult<TResult>> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        cancellationToken.ThrowIfCancellationRequested();
        var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
        if (!lease.Accepted)
            return lease.Outcome switch
            {
                BackpressureOutcome.Dropped => BackpressureResult<TResult>.DroppedResult(ActiveCount, DroppedCount),
                BackpressureOutcome.Shed => BackpressureResult<TResult>.ShedResult(ActiveCount, DroppedCount),
                _ => BackpressureResult<TResult>.RejectedResult(ActiveCount, DroppedCount)
            };

        try
        {
            var value = await operation(cancellationToken).ConfigureAwait(false);
            return lease.Observed
                ? BackpressureResult<TResult>.ObservedResult(value, ActiveCount, DroppedCount)
                : BackpressureResult<TResult>.AcceptedResult(value, lease.Waited, ActiveCount, DroppedCount);
        }
        finally
        {
            if (!lease.Observed)
                Exit();
        }
    }

    private BackpressureLease Enter()
    {
        if (_slots.Wait(0))
            return Start(waited: false);

        return Mode switch
        {
            BackpressureMode.Wait => WaitForSlot(),
            BackpressureMode.DropNewest => Drop(BackpressureOutcome.Dropped),
            BackpressureMode.DropOldest => Drop(BackpressureOutcome.Dropped),
            BackpressureMode.Shed => BackpressureLease.Denied(BackpressureOutcome.Shed),
            BackpressureMode.Observe => BackpressureLease.CreateObserved(),
            _ => BackpressureLease.Denied(BackpressureOutcome.Rejected)
        };
    }

    private async ValueTask<BackpressureLease> EnterAsync(CancellationToken cancellationToken)
    {
        if (await _slots.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return Start(waited: false);

        return Mode switch
        {
            BackpressureMode.Wait => await WaitForSlotAsync(cancellationToken).ConfigureAwait(false),
            BackpressureMode.DropNewest => Drop(BackpressureOutcome.Dropped),
            BackpressureMode.DropOldest => Drop(BackpressureOutcome.Dropped),
            BackpressureMode.Shed => BackpressureLease.Denied(BackpressureOutcome.Shed),
            BackpressureMode.Observe => BackpressureLease.CreateObserved(),
            _ => BackpressureLease.Denied(BackpressureOutcome.Rejected)
        };
    }

    private BackpressureLease WaitForSlot()
        => _slots.Wait(WaitTimeout)
            ? Start(waited: true)
            : BackpressureLease.Denied(BackpressureOutcome.Rejected);

    private async ValueTask<BackpressureLease> WaitForSlotAsync(CancellationToken cancellationToken)
        => await _slots.WaitAsync(WaitTimeout, cancellationToken).ConfigureAwait(false)
            ? Start(waited: true)
            : BackpressureLease.Denied(BackpressureOutcome.Rejected);

    private BackpressureLease Start(bool waited)
    {
        Interlocked.Increment(ref _activeCount);
        return BackpressureLease.CreateAccepted(waited);
    }

    private BackpressureLease Drop(BackpressureOutcome outcome)
    {
        Interlocked.Increment(ref _droppedCount);
        return BackpressureLease.Denied(outcome);
    }

    private void Exit()
    {
        Interlocked.Decrement(ref _activeCount);
        _slots.Release();
    }

    private static bool IsDefinedMode(BackpressureMode mode)
        => mode is BackpressureMode.Reject
            or BackpressureMode.Wait
            or BackpressureMode.DropNewest
            or BackpressureMode.DropOldest
            or BackpressureMode.Shed
            or BackpressureMode.Observe;

    public sealed class Builder
    {
        private readonly string _name;
        private int _capacity = 8;
        private BackpressureMode _mode = BackpressureMode.Reject;
        private TimeSpan _waitTimeout = TimeSpan.Zero;

        internal Builder(string name) => _name = name;

        public Builder WithCapacity(int capacity)
        {
            _capacity = capacity;
            return this;
        }

        public Builder WithMode(BackpressureMode mode)
        {
            _mode = mode;
            return this;
        }

        public Builder WithWaitTimeout(TimeSpan waitTimeout)
        {
            _waitTimeout = waitTimeout;
            return this;
        }

        public BackpressurePolicy<TResult> Build()
            => new(_name, _capacity, _mode, _waitTimeout);
    }

    private enum BackpressureOutcome
    {
        Rejected,
        Dropped,
        Shed
    }

    private readonly struct BackpressureLease
    {
        private BackpressureLease(bool accepted, bool waited, bool observed, BackpressureOutcome outcome)
        {
            Accepted = accepted;
            Waited = waited;
            Observed = observed;
            Outcome = outcome;
        }

        public bool Accepted { get; }
        public bool Waited { get; }
        public bool Observed { get; }
        public BackpressureOutcome Outcome { get; }

        public static BackpressureLease CreateAccepted(bool waited) => new(true, waited, false, BackpressureOutcome.Rejected);
        public static BackpressureLease CreateObserved() => new(true, false, true, BackpressureOutcome.Rejected);
        public static BackpressureLease Denied(BackpressureOutcome outcome) => new(false, false, false, outcome);
    }
}
