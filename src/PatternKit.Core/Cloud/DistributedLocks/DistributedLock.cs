namespace PatternKit.Cloud.DistributedLocks;

/// <summary>
/// Coordinates mutually exclusive ownership of named resources through expiring leases.
/// </summary>
public sealed class DistributedLock<TKey>
    where TKey : notnull
{
    private readonly object _gate = new();
    private readonly Dictionary<TKey, DistributedLockRecord<TKey>> _locks;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _leaseDuration;

    private DistributedLock(string name, TimeSpan leaseDuration, IEqualityComparer<TKey> keyComparer, Func<DateTimeOffset> clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Distributed lock name cannot be null, empty, or whitespace.", nameof(name));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");

        Name = name;
        _leaseDuration = leaseDuration;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _locks = new Dictionary<TKey, DistributedLockRecord<TKey>>(keyComparer ?? throw new ArgumentNullException(nameof(keyComparer)));
    }

    public string Name { get; }

    public bool IsBlocked => ActiveCount > 0;

    public int ActiveCount
    {
        get
        {
            lock (_gate)
            {
                ExpireStaleLocks(_clock());
                return _locks.Count;
            }
        }
    }

    public DistributedLockResult<TKey> TryAcquire(TKey resourceKey, string ownerId)
        => TryAcquire(resourceKey, ownerId, null);

    public DistributedLockResult<TKey> TryAcquire(TKey resourceKey, string ownerId, TimeSpan? leaseDuration)
    {
        ValidateOwner(ownerId);
        var duration = leaseDuration ?? _leaseDuration;
        ValidateLeaseDuration(duration);

        lock (_gate)
        {
            var now = _clock();
            ExpireStaleLocks(now);
            if (_locks.TryGetValue(resourceKey, out var current))
                return DistributedLockResult<TKey>.Failure(Name, resourceKey, ownerId, new InvalidOperationException($"Resource is locked by '{current.OwnerId}'."), current);

            var record = new DistributedLockRecord<TKey>(resourceKey, ownerId, Guid.NewGuid().ToString("N"), now, now.Add(duration));
            _locks[resourceKey] = record;
            return DistributedLockResult<TKey>.Acquisition(Name, record);
        }
    }

    public DistributedLockResult<TKey> Renew(DistributedLockRecord<TKey> lease)
        => Renew(lease, null);

    public DistributedLockResult<TKey> Renew(DistributedLockRecord<TKey> lease, TimeSpan? leaseDuration)
    {
        if (lease is null)
            throw new ArgumentNullException(nameof(lease));

        var duration = leaseDuration ?? _leaseDuration;
        ValidateLeaseDuration(duration);

        lock (_gate)
        {
            var now = _clock();
            ExpireStaleLocks(now);
            if (!_locks.TryGetValue(lease.ResourceKey, out var current))
                return DistributedLockResult<TKey>.Failure(Name, lease.ResourceKey, lease.OwnerId, new InvalidOperationException("No active lease exists."));
            if (!current.Matches(lease))
                return DistributedLockResult<TKey>.Failure(Name, lease.ResourceKey, lease.OwnerId, new InvalidOperationException("Lease token is not current."), current);

            var renewed = current.Renew(now.Add(duration));
            _locks[lease.ResourceKey] = renewed;
            return DistributedLockResult<TKey>.Renewal(Name, renewed);
        }
    }

    public DistributedLockResult<TKey> Release(DistributedLockRecord<TKey> lease)
    {
        if (lease is null)
            throw new ArgumentNullException(nameof(lease));

        lock (_gate)
        {
            ExpireStaleLocks(_clock());
            if (!_locks.TryGetValue(lease.ResourceKey, out var current))
                return DistributedLockResult<TKey>.Failure(Name, lease.ResourceKey, lease.OwnerId, new InvalidOperationException("No active lease exists."));
            if (!current.Matches(lease))
                return DistributedLockResult<TKey>.Failure(Name, lease.ResourceKey, lease.OwnerId, new InvalidOperationException("Lease token is not current."), current);

            _locks.Remove(lease.ResourceKey);
            return DistributedLockResult<TKey>.Release(Name, current);
        }
    }

    public bool IsLocked(TKey resourceKey)
    {
        lock (_gate)
        {
            ExpireStaleLocks(_clock());
            return _locks.ContainsKey(resourceKey);
        }
    }

    public IReadOnlyList<DistributedLockRecord<TKey>> Snapshot()
    {
        lock (_gate)
        {
            ExpireStaleLocks(_clock());
            return _locks.Values
                .OrderBy(static lease => lease.AcquiredAt)
                .ThenBy(static lease => lease.Token, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public DistributedLockState<TKey> GetState()
    {
        var leases = Snapshot();
        return new(Name, leases.Count > 0, leases.Count, leases);
    }

    public static Builder Create(string name = "distributed-lock") => new(name);

    private void ExpireStaleLocks(DateTimeOffset now)
    {
        foreach (var key in _locks.Where(pair => pair.Value.ExpiresAt <= now).Select(static pair => pair.Key).ToArray())
            _locks.Remove(key);
    }

    private static void ValidateOwner(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Lock owner id cannot be null, empty, or whitespace.", nameof(ownerId));
    }

    private static void ValidateLeaseDuration(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
    }

    public sealed class Builder
    {
        private readonly string _name;
        private TimeSpan _leaseDuration = TimeSpan.FromSeconds(30);
        private IEqualityComparer<TKey> _keyComparer = EqualityComparer<TKey>.Default;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;

        internal Builder(string name) => _name = name;

        public Builder LeaseDuration(TimeSpan leaseDuration)
        {
            _leaseDuration = leaseDuration;
            return this;
        }

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

        public DistributedLock<TKey> Build() => new(_name, _leaseDuration, _keyComparer, _clock);
    }
}

public sealed class DistributedLockRecord<TKey>
    where TKey : notnull
{
    public DistributedLockRecord(TKey resourceKey, string ownerId, string token, DateTimeOffset acquiredAt, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Lock owner id cannot be null, empty, or whitespace.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Lock token cannot be null, empty, or whitespace.", nameof(token));

        ResourceKey = resourceKey;
        OwnerId = ownerId;
        Token = token;
        AcquiredAt = acquiredAt;
        ExpiresAt = expiresAt;
    }

    public TKey ResourceKey { get; }

    public string OwnerId { get; }

    public string Token { get; }

    public DateTimeOffset AcquiredAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public DistributedLockRecord<TKey> Renew(DateTimeOffset expiresAt)
        => new(ResourceKey, OwnerId, Token, AcquiredAt, expiresAt);

    internal bool Matches(DistributedLockRecord<TKey> lease)
        => string.Equals(OwnerId, lease.OwnerId, StringComparison.Ordinal)
           && string.Equals(Token, lease.Token, StringComparison.Ordinal);
}

public sealed class DistributedLockResult<TKey>
    where TKey : notnull
{
    private DistributedLockResult(string lockName, TKey resourceKey, string ownerId, DistributedLockRecord<TKey>? lease, Exception? exception, bool acquired, bool renewed, bool released)
        => (LockName, ResourceKey, OwnerId, Lease, Exception, Acquired, Renewed, Released) = (lockName, resourceKey, ownerId, lease, exception, acquired, renewed, released);

    public string LockName { get; }

    public TKey ResourceKey { get; }

    public string OwnerId { get; }

    public DistributedLockRecord<TKey>? Lease { get; }

    public Exception? Exception { get; }

    public bool Acquired { get; }

    public bool Renewed { get; }

    public bool Released { get; }

    public bool Succeeded => Exception is null;

    public bool Failed => !Succeeded;

    public static DistributedLockResult<TKey> Acquisition(string lockName, DistributedLockRecord<TKey> lease)
        => new(lockName, lease.ResourceKey, lease.OwnerId, lease, null, acquired: true, renewed: false, released: false);

    public static DistributedLockResult<TKey> Renewal(string lockName, DistributedLockRecord<TKey> lease)
        => new(lockName, lease.ResourceKey, lease.OwnerId, lease, null, acquired: false, renewed: true, released: false);

    public static DistributedLockResult<TKey> Release(string lockName, DistributedLockRecord<TKey> lease)
        => new(lockName, lease.ResourceKey, lease.OwnerId, lease, null, acquired: false, renewed: false, released: true);

    public static DistributedLockResult<TKey> Failure(string lockName, TKey resourceKey, string ownerId, Exception exception, DistributedLockRecord<TKey>? lease = null)
        => new(lockName, resourceKey, ownerId, lease, exception ?? throw new ArgumentNullException(nameof(exception)), acquired: false, renewed: false, released: false);
}

public sealed class DistributedLockState<TKey>
    where TKey : notnull
{
    public DistributedLockState(string lockName, bool isBlocked, int activeCount, IReadOnlyList<DistributedLockRecord<TKey>> activeLeases)
    {
        LockName = lockName;
        IsBlocked = isBlocked;
        ActiveCount = activeCount;
        ActiveLeases = activeLeases ?? throw new ArgumentNullException(nameof(activeLeases));
    }

    public string LockName { get; }

    public bool IsBlocked { get; }

    public int ActiveCount { get; }

    public IReadOnlyList<DistributedLockRecord<TKey>> ActiveLeases { get; }
}
