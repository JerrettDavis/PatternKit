namespace PatternKit.Messaging.Reliability;

/// <summary>Status for a guaranteed-delivery record.</summary>
public enum GuaranteedDeliveryStatus
{
    Pending,
    Leased,
    Delivered,
    DeadLettered,
}

/// <summary>Stored message record used by the Guaranteed Delivery pattern.</summary>
public sealed class GuaranteedDeliveryRecord<TPayload>
{
    public GuaranteedDeliveryRecord(string id, Message<TPayload> message, DateTimeOffset enqueuedAt)
        : this(id, message, enqueuedAt, GuaranteedDeliveryStatus.Pending, 0, null, null)
    {
    }

    private GuaranteedDeliveryRecord(
        string id,
        Message<TPayload> message,
        DateTimeOffset enqueuedAt,
        GuaranteedDeliveryStatus status,
        int attempts,
        DateTimeOffset? leasedUntil,
        string? lastError)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Delivery record id is required.", nameof(id))
            : id;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        EnqueuedAt = enqueuedAt;
        Status = status;
        Attempts = attempts;
        LeasedUntil = leasedUntil;
        LastError = lastError;
    }

    public string Id { get; }
    public Message<TPayload> Message { get; }
    public DateTimeOffset EnqueuedAt { get; }
    public GuaranteedDeliveryStatus Status { get; }
    public int Attempts { get; }
    public DateTimeOffset? LeasedUntil { get; }
    public string? LastError { get; }

    public GuaranteedDeliveryRecord<TPayload> Lease(DateTimeOffset leasedUntil)
        => new(Id, Message, EnqueuedAt, GuaranteedDeliveryStatus.Leased, Attempts + 1, leasedUntil, null);

    public GuaranteedDeliveryRecord<TPayload> Acknowledge()
        => new(Id, Message, EnqueuedAt, GuaranteedDeliveryStatus.Delivered, Attempts, null, null);

    public GuaranteedDeliveryRecord<TPayload> Release(string? error = null)
        => new(Id, Message, EnqueuedAt, GuaranteedDeliveryStatus.Pending, Attempts, null, error);

    public GuaranteedDeliveryRecord<TPayload> DeadLetter(string? error = null)
        => new(Id, Message, EnqueuedAt, GuaranteedDeliveryStatus.DeadLettered, Attempts, null, error);
}

/// <summary>A lease returned when a guaranteed-delivery message is received for processing.</summary>
public sealed class GuaranteedDeliveryLease<TPayload>
{
    public GuaranteedDeliveryLease(GuaranteedDeliveryRecord<TPayload> record)
        => Record = record ?? throw new ArgumentNullException(nameof(record));

    public GuaranteedDeliveryRecord<TPayload> Record { get; }
    public string Id => Record.Id;
    public Message<TPayload> Message => Record.Message;
}

/// <summary>Backing store contract for guaranteed-delivery queues.</summary>
public interface IGuaranteedDeliveryStore<TPayload>
{
    ValueTask<GuaranteedDeliveryRecord<TPayload>> EnqueueAsync(
        Message<TPayload> message,
        string? id = null,
        DateTimeOffset? enqueuedAt = null,
        CancellationToken cancellationToken = default);

    ValueTask<GuaranteedDeliveryLease<TPayload>?> TryLeaseAsync(
        TimeSpan leaseDuration,
        int maxDeliveryAttempts,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    ValueTask AcknowledgeAsync(string id, CancellationToken cancellationToken = default);

    ValueTask ReleaseAsync(string id, string? error = null, CancellationToken cancellationToken = default);

    ValueTask DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<GuaranteedDeliveryRecord<TPayload>>> SnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>Thread-safe in-memory store for tests, demos, and single-process applications.</summary>
public sealed class InMemoryGuaranteedDeliveryStore<TPayload> : IGuaranteedDeliveryStore<TPayload>
{
    private readonly object _gate = new();
    private readonly List<GuaranteedDeliveryRecord<TPayload>> _records = new();

    public IReadOnlyList<GuaranteedDeliveryRecord<TPayload>> Records
    {
        get
        {
            lock (_gate)
                return _records.ToArray();
        }
    }

    public ValueTask<GuaranteedDeliveryRecord<TPayload>> EnqueueAsync(
        Message<TPayload> message,
        string? id = null,
        DateTimeOffset? enqueuedAt = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        cancellationToken.ThrowIfCancellationRequested();

        var record = new GuaranteedDeliveryRecord<TPayload>(
            string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!,
            message,
            enqueuedAt ?? DateTimeOffset.UtcNow);

        lock (_gate)
            _records.Add(record);

        return new ValueTask<GuaranteedDeliveryRecord<TPayload>>(record);
    }

    public ValueTask<GuaranteedDeliveryLease<TPayload>?> TryLeaseAsync(
        TimeSpan leaseDuration,
        int maxDeliveryAttempts,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");
        if (maxDeliveryAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDeliveryAttempts), "Max delivery attempts must be positive.");

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            for (var i = 0; i < _records.Count; i++)
            {
                var record = _records[i];
                if (record.Status == GuaranteedDeliveryStatus.Delivered ||
                    record.Status == GuaranteedDeliveryStatus.DeadLettered)
                    continue;

                if (record.Status == GuaranteedDeliveryStatus.Leased && record.LeasedUntil > now)
                    continue;

                if (record.Attempts >= maxDeliveryAttempts)
                {
                    _records[i] = record.DeadLetter("Maximum delivery attempts exceeded.");
                    continue;
                }

                var leased = record.Lease(now.Add(leaseDuration));
                _records[i] = leased;
                return new ValueTask<GuaranteedDeliveryLease<TPayload>?>(new GuaranteedDeliveryLease<TPayload>(leased));
            }
        }

        return new ValueTask<GuaranteedDeliveryLease<TPayload>?>(default(GuaranteedDeliveryLease<TPayload>));
    }

    public ValueTask AcknowledgeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Delivery record id is required.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();
        Replace(id, static record => record.Acknowledge());
        return default;
    }

    public ValueTask ReleaseAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Delivery record id is required.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();
        Replace(id, record => record.Release(error));
        return default;
    }

    public ValueTask DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Delivery record id is required.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();
        Replace(id, record => record.DeadLetter(error));
        return default;
    }

    public ValueTask<IReadOnlyList<GuaranteedDeliveryRecord<TPayload>>> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<GuaranteedDeliveryRecord<TPayload>> snapshot = _records.ToArray();
            return new ValueTask<IReadOnlyList<GuaranteedDeliveryRecord<TPayload>>>(snapshot);
        }
    }

    private void Replace(string id, Func<GuaranteedDeliveryRecord<TPayload>, GuaranteedDeliveryRecord<TPayload>> update)
    {
        lock (_gate)
        {
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == id)
                {
                    _records[i] = update(_records[i]);
                    return;
                }
            }
        }
    }
}

/// <summary>
/// Durable queue facade for the Guaranteed Delivery enterprise integration pattern.
/// </summary>
public sealed class GuaranteedDeliveryQueue<TPayload>
{
    private readonly IGuaranteedDeliveryStore<TPayload> _store;
    private readonly Func<DateTimeOffset> _clock;

    private GuaranteedDeliveryQueue(
        string name,
        IGuaranteedDeliveryStore<TPayload> store,
        TimeSpan leaseDuration,
        int maxDeliveryAttempts,
        Func<DateTimeOffset> clock)
    {
        Name = name;
        _store = store;
        LeaseDuration = leaseDuration;
        MaxDeliveryAttempts = maxDeliveryAttempts;
        _clock = clock;
    }

    public string Name { get; }
    public TimeSpan LeaseDuration { get; }
    public int MaxDeliveryAttempts { get; }

    public static Builder Create(IGuaranteedDeliveryStore<TPayload> store) => new(store);

    public ValueTask<GuaranteedDeliveryRecord<TPayload>> EnqueueAsync(
        Message<TPayload> message,
        string? id = null,
        DateTimeOffset? enqueuedAt = null,
        CancellationToken cancellationToken = default)
        => _store.EnqueueAsync(message, id, enqueuedAt, cancellationToken);

    public ValueTask<GuaranteedDeliveryLease<TPayload>?> TryReceiveAsync(CancellationToken cancellationToken = default)
        => _store.TryLeaseAsync(LeaseDuration, MaxDeliveryAttempts, _clock(), cancellationToken);

    public ValueTask AcknowledgeAsync(GuaranteedDeliveryLease<TPayload> lease, CancellationToken cancellationToken = default)
    {
        if (lease is null)
            throw new ArgumentNullException(nameof(lease));

        return _store.AcknowledgeAsync(lease.Id, cancellationToken);
    }

    public ValueTask ReleaseAsync(GuaranteedDeliveryLease<TPayload> lease, string? error = null, CancellationToken cancellationToken = default)
    {
        if (lease is null)
            throw new ArgumentNullException(nameof(lease));

        return _store.ReleaseAsync(lease.Id, error, cancellationToken);
    }

    public ValueTask DeadLetterAsync(GuaranteedDeliveryLease<TPayload> lease, string? error = null, CancellationToken cancellationToken = default)
    {
        if (lease is null)
            throw new ArgumentNullException(nameof(lease));

        return _store.DeadLetterAsync(lease.Id, error, cancellationToken);
    }

    public ValueTask<IReadOnlyList<GuaranteedDeliveryRecord<TPayload>>> SnapshotAsync(CancellationToken cancellationToken = default)
        => _store.SnapshotAsync(cancellationToken);

    public sealed class Builder
    {
        private readonly IGuaranteedDeliveryStore<TPayload> _store;
        private string _name = "guaranteed-delivery";
        private TimeSpan _leaseDuration = TimeSpan.FromMinutes(5);
        private int _maxDeliveryAttempts = 5;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;

        internal Builder(IGuaranteedDeliveryStore<TPayload> store)
            => _store = store ?? throw new ArgumentNullException(nameof(store));

        public Builder Name(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Queue name is required.", nameof(name))
                : name;
            return this;
        }

        public Builder LeaseDuration(TimeSpan leaseDuration)
        {
            if (leaseDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");

            _leaseDuration = leaseDuration;
            return this;
        }

        public Builder MaxDeliveryAttempts(int maxDeliveryAttempts)
        {
            if (maxDeliveryAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxDeliveryAttempts), "Max delivery attempts must be positive.");

            _maxDeliveryAttempts = maxDeliveryAttempts;
            return this;
        }

        public Builder Clock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public GuaranteedDeliveryQueue<TPayload> Build()
            => new(_name, _store, _leaseDuration, _maxDeliveryAttempts, _clock);
    }
}
