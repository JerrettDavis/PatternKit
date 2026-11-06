namespace PatternKit.Cloud.QueueLoadLeveling;

public sealed class QueueLoadLevelingResult<TResult>
{
    public QueueLoadLevelingResult(TResult? value, bool accepted, bool rejected, bool timedOut, bool queued, int availableWorkers)
    {
        Value = value;
        Accepted = accepted;
        Rejected = rejected;
        TimedOut = timedOut;
        Queued = queued;
        AvailableWorkers = availableWorkers;
    }

    public TResult? Value { get; }

    public bool Accepted { get; }

    public bool Rejected { get; }

    public bool TimedOut { get; }

    public bool Queued { get; }

    public int AvailableWorkers { get; }

    public static QueueLoadLevelingResult<TResult> Success(TResult value, bool queued, int availableWorkers)
        => new(value, true, false, false, queued, availableWorkers);

    public static QueueLoadLevelingResult<TResult> Rejection(int availableWorkers)
        => new(default, false, true, false, false, availableWorkers);

    public static QueueLoadLevelingResult<TResult> Timeout(int availableWorkers)
        => new(default, false, false, true, true, availableWorkers);
}

public sealed class QueueLoadLevelingPolicy<TResult>
{
    private readonly SemaphoreSlim _workers;
    private readonly object _gate = new();
    private int _queued;

    private QueueLoadLevelingPolicy(string name, int maxConcurrentWorkers, int maxQueueLength, TimeSpan queueTimeout)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Queue load leveling policy name is required.", nameof(name));
        if (maxConcurrentWorkers < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentWorkers), maxConcurrentWorkers, "Queue load leveling worker count must be at least one.");
        if (maxQueueLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueueLength), maxQueueLength, "Queue load leveling queue length cannot be negative.");
        if (queueTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(queueTimeout), queueTimeout, "Queue load leveling timeout cannot be negative.");

        Name = name;
        MaxConcurrentWorkers = maxConcurrentWorkers;
        MaxQueueLength = maxQueueLength;
        QueueTimeout = queueTimeout;
        _workers = new SemaphoreSlim(maxConcurrentWorkers, maxConcurrentWorkers);
    }

    public string Name { get; }

    public int MaxConcurrentWorkers { get; }

    public int MaxQueueLength { get; }

    public TimeSpan QueueTimeout { get; }

    public int AvailableWorkers => _workers.CurrentCount;

    public int QueuedCount
    {
        get
        {
            lock (_gate)
                return _queued;
        }
    }

    public static Builder Create(string name = "queue-load-leveling") => new(name);

    public QueueLoadLevelingResult<TResult> Execute(Func<TResult> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var lease = Enter();
        if (!lease.Allowed)
            return lease.TimedOut
                ? QueueLoadLevelingResult<TResult>.Timeout(AvailableWorkers)
                : QueueLoadLevelingResult<TResult>.Rejection(AvailableWorkers);

        try
        {
            return QueueLoadLevelingResult<TResult>.Success(operation(), lease.Queued, AvailableWorkers);
        }
        finally
        {
            _workers.Release();
        }
    }

    public async ValueTask<QueueLoadLevelingResult<TResult>> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        cancellationToken.ThrowIfCancellationRequested();
        var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
        if (!lease.Allowed)
            return lease.TimedOut
                ? QueueLoadLevelingResult<TResult>.Timeout(AvailableWorkers)
                : QueueLoadLevelingResult<TResult>.Rejection(AvailableWorkers);

        try
        {
            var value = await operation(cancellationToken).ConfigureAwait(false);
            return QueueLoadLevelingResult<TResult>.Success(value, lease.Queued, AvailableWorkers);
        }
        finally
        {
            _workers.Release();
        }
    }

    private QueueLease Enter()
    {
        if (_workers.Wait(0))
            return QueueLease.AllowedImmediate();

        if (!TryReserveQueue())
            return QueueLease.Rejected();

        try
        {
            return _workers.Wait(QueueTimeout)
                ? QueueLease.AllowedQueued()
                : QueueLease.Timeout();
        }
        finally
        {
            ReleaseQueueReservation();
        }
    }

    private async ValueTask<QueueLease> EnterAsync(CancellationToken cancellationToken)
    {
        if (await _workers.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return QueueLease.AllowedImmediate();

        if (!TryReserveQueue())
            return QueueLease.Rejected();

        try
        {
            return await _workers.WaitAsync(QueueTimeout, cancellationToken).ConfigureAwait(false)
                ? QueueLease.AllowedQueued()
                : QueueLease.Timeout();
        }
        finally
        {
            ReleaseQueueReservation();
        }
    }

    private bool TryReserveQueue()
    {
        lock (_gate)
        {
            if (_queued >= MaxQueueLength)
                return false;

            _queued++;
            return true;
        }
    }

    private void ReleaseQueueReservation()
    {
        lock (_gate)
            _queued--;
    }

    public sealed class Builder
    {
        private readonly string _name;
        private int _maxConcurrentWorkers = 1;
        private int _maxQueueLength = 100;
        private TimeSpan _queueTimeout = TimeSpan.FromSeconds(30);

        internal Builder(string name) => _name = name;

        public Builder WithMaxConcurrentWorkers(int maxConcurrentWorkers)
        {
            _maxConcurrentWorkers = maxConcurrentWorkers;
            return this;
        }

        public Builder WithMaxQueueLength(int maxQueueLength)
        {
            _maxQueueLength = maxQueueLength;
            return this;
        }

        public Builder WithQueueTimeout(TimeSpan queueTimeout)
        {
            _queueTimeout = queueTimeout;
            return this;
        }

        public QueueLoadLevelingPolicy<TResult> Build()
            => new(_name, _maxConcurrentWorkers, _maxQueueLength, _queueTimeout);
    }

    private readonly struct QueueLease
    {
        private QueueLease(bool allowed, bool queued, bool timedOut)
        {
            Allowed = allowed;
            Queued = queued;
            TimedOut = timedOut;
        }

        public bool Allowed { get; }

        public bool Queued { get; }

        public bool TimedOut { get; }

        public static QueueLease AllowedImmediate() => new(true, false, false);

        public static QueueLease AllowedQueued() => new(true, true, false);

        public static QueueLease Rejected() => new(false, false, false);

        public static QueueLease Timeout() => new(false, true, true);
    }
}
