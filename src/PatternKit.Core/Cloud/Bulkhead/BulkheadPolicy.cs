namespace PatternKit.Cloud.Bulkhead;

/// <summary>
/// Outcome returned by a bulkhead policy execution.
/// </summary>
public sealed class BulkheadResult<TResult>
{
    public BulkheadResult(TResult? value, bool succeeded, bool rejected, bool timedOut, bool queued, int availableSlots)
    {
        Value = value;
        Succeeded = succeeded;
        Rejected = rejected;
        TimedOut = timedOut;
        Queued = queued;
        AvailableSlots = availableSlots;
    }

    public TResult? Value { get; }
    public bool Succeeded { get; }
    public bool Rejected { get; }
    public bool TimedOut { get; }
    public bool Queued { get; }
    public int AvailableSlots { get; }

    public static BulkheadResult<TResult> Success(TResult value, bool queued, int availableSlots)
        => new(value, true, false, false, queued, availableSlots);

    public static BulkheadResult<TResult> Rejection(int availableSlots)
        => new(default, false, true, false, false, availableSlots);

    public static BulkheadResult<TResult> Timeout(int availableSlots)
        => new(default, false, false, true, true, availableSlots);
}

/// <summary>
/// Bulkhead policy for isolating dependency calls behind concurrency and queue limits.
/// </summary>
public sealed class BulkheadPolicy<TResult>
{
    private readonly SemaphoreSlim _slots;
    private readonly object _gate = new();
    private int _queued;

    private BulkheadPolicy(string name, int maxConcurrency, int maxQueueLength, TimeSpan queueTimeout)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Bulkhead policy name is required.", nameof(name));
        if (maxConcurrency < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Bulkhead max concurrency must be at least one.");
        if (maxQueueLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueueLength), maxQueueLength, "Bulkhead max queue length cannot be negative.");
        if (queueTimeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(queueTimeout), queueTimeout, "Bulkhead queue timeout cannot be negative.");

        Name = name;
        MaxConcurrency = maxConcurrency;
        MaxQueueLength = maxQueueLength;
        QueueTimeout = queueTimeout;
        _slots = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public string Name { get; }
    public int MaxConcurrency { get; }
    public int MaxQueueLength { get; }
    public TimeSpan QueueTimeout { get; }
    public int AvailableSlots => _slots.CurrentCount;
    public int QueuedCount
    {
        get
        {
            lock (_gate)
                return _queued;
        }
    }

    public static Builder Create(string name = "bulkhead") => new(name);

    public BulkheadResult<TResult> Execute(Func<TResult> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var lease = Enter();
        if (!lease.Allowed)
            return lease.TimedOut
                ? BulkheadResult<TResult>.Timeout(AvailableSlots)
                : BulkheadResult<TResult>.Rejection(AvailableSlots);

        try
        {
            var value = operation();
            return BulkheadResult<TResult>.Success(value, lease.Queued, AvailableSlots);
        }
        finally
        {
            _slots.Release();
        }
    }

    public async ValueTask<BulkheadResult<TResult>> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        cancellationToken.ThrowIfCancellationRequested();
        var lease = await EnterAsync(cancellationToken).ConfigureAwait(false);
        if (!lease.Allowed)
            return lease.TimedOut
                ? BulkheadResult<TResult>.Timeout(AvailableSlots)
                : BulkheadResult<TResult>.Rejection(AvailableSlots);

        try
        {
            var value = await operation(cancellationToken).ConfigureAwait(false);
            return BulkheadResult<TResult>.Success(value, lease.Queued, AvailableSlots);
        }
        finally
        {
            _slots.Release();
        }
    }

    private BulkheadLease Enter()
    {
        if (_slots.Wait(0))
            return BulkheadLease.CreateAllowed(queued: false);

        if (!TryReserveQueue())
            return BulkheadLease.Rejected();

        try
        {
            return _slots.Wait(QueueTimeout)
                ? BulkheadLease.CreateAllowed(queued: true)
                : BulkheadLease.Timeout();
        }
        finally
        {
            ReleaseQueueReservation();
        }
    }

    private async ValueTask<BulkheadLease> EnterAsync(CancellationToken cancellationToken)
    {
        if (await _slots.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return BulkheadLease.CreateAllowed(queued: false);

        if (!TryReserveQueue())
            return BulkheadLease.Rejected();

        try
        {
            return await _slots.WaitAsync(QueueTimeout, cancellationToken).ConfigureAwait(false)
                ? BulkheadLease.CreateAllowed(queued: true)
                : BulkheadLease.Timeout();
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
        private int _maxConcurrency = 8;
        private int _maxQueueLength;
        private TimeSpan _queueTimeout = TimeSpan.Zero;

        internal Builder(string name) => _name = name;

        public Builder WithMaxConcurrency(int maxConcurrency)
        {
            _maxConcurrency = maxConcurrency;
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

        public BulkheadPolicy<TResult> Build()
            => new(_name, _maxConcurrency, _maxQueueLength, _queueTimeout);
    }

    private readonly struct BulkheadLease
    {
        private BulkheadLease(bool allowed, bool queued, bool timedOut)
        {
            Allowed = allowed;
            Queued = queued;
            TimedOut = timedOut;
        }

        public bool Allowed { get; }
        public bool Queued { get; }
        public bool TimedOut { get; }

        public static BulkheadLease CreateAllowed(bool queued) => new(true, queued, false);
        public static BulkheadLease Rejected() => new(false, false, false);
        public static BulkheadLease Timeout() => new(false, true, true);
    }
}
