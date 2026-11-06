namespace PatternKit.Cloud.RateLimiting;

/// <summary>
/// Outcome returned by a rate-limit policy execution.
/// </summary>
public sealed class RateLimitResult<TResult>
{
    private RateLimitResult(string key, TResult? value, bool allowed, int remainingPermits, DateTimeOffset? retryAfter)
    {
        Key = key;
        Value = value;
        Allowed = allowed;
        RemainingPermits = remainingPermits;
        RetryAfter = retryAfter;
    }

    public string Key { get; }
    public TResult? Value { get; }
    public bool Allowed { get; }
    public bool Rejected => !Allowed;
    public int RemainingPermits { get; }
    public DateTimeOffset? RetryAfter { get; }

    public static RateLimitResult<TResult> Success(string key, TResult value, int remainingPermits)
        => new(key, value, true, remainingPermits, null);

    public static RateLimitResult<TResult> Rejection(string key, int remainingPermits, DateTimeOffset retryAfter)
        => new(key, default, false, remainingPermits, retryAfter);
}

/// <summary>
/// Fixed-window, key-partitioned rate-limit policy for guarding tenant, user, or resource operations.
/// </summary>
public sealed class RateLimitPolicy<TResult>
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WindowState> _windows;
    private readonly Func<DateTimeOffset> _clock;

    private RateLimitPolicy(string name, int permitLimit, TimeSpan window, Func<DateTimeOffset> clock, IEqualityComparer<string> keyComparer)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rate-limit policy name is required.", nameof(name));
        if (permitLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(permitLimit), permitLimit, "Permit limit must be at least one.");
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), window, "Rate-limit window must be greater than zero.");

        Name = name;
        PermitLimit = permitLimit;
        Window = window;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _windows = new Dictionary<string, WindowState>(keyComparer ?? throw new ArgumentNullException(nameof(keyComparer)));
    }

    public string Name { get; }
    public int PermitLimit { get; }
    public TimeSpan Window { get; }

    public static Builder Create(string name = "rate-limit") => new(name);

    public RateLimitResult<TResult> Execute(string key, Func<TResult> operation)
    {
        ValidateKey(key);
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var lease = TryAcquire(key);
        if (!lease.Allowed)
            return RateLimitResult<TResult>.Rejection(key, 0, lease.RetryAfter);

        var value = operation();
        return RateLimitResult<TResult>.Success(key, value, lease.RemainingPermits);
    }

    public async ValueTask<RateLimitResult<TResult>> ExecuteAsync(
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        cancellationToken.ThrowIfCancellationRequested();
        var lease = TryAcquire(key);
        if (!lease.Allowed)
            return RateLimitResult<TResult>.Rejection(key, 0, lease.RetryAfter);

        var value = await operation(cancellationToken).ConfigureAwait(false);
        return RateLimitResult<TResult>.Success(key, value, lease.RemainingPermits);
    }

    public bool Reset(string key)
    {
        ValidateKey(key);

        lock (_gate)
            return _windows.Remove(key);
    }

    public void Clear()
    {
        lock (_gate)
            _windows.Clear();
    }

    private Lease TryAcquire(string key)
    {
        var now = _clock();

        lock (_gate)
        {
            if (!_windows.TryGetValue(key, out var state) || now - state.WindowStart >= Window)
            {
                state = new WindowState(now);
                _windows[key] = state;
            }

            if (state.Used >= PermitLimit)
                return Lease.Rejected(state.WindowStart.Add(Window));

            state.Used++;
            return Lease.AllowedLease(PermitLimit - state.Used);
        }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Rate-limit key is required.", nameof(key));
    }

    private static DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;

    private sealed class WindowState
    {
        public WindowState(DateTimeOffset windowStart) => WindowStart = windowStart;

        public DateTimeOffset WindowStart { get; }
        public int Used { get; set; }
    }

    private readonly struct Lease
    {
        private Lease(bool allowed, int remainingPermits, DateTimeOffset retryAfter)
        {
            Allowed = allowed;
            RemainingPermits = remainingPermits;
            RetryAfter = retryAfter;
        }

        public bool Allowed { get; }
        public int RemainingPermits { get; }
        public DateTimeOffset RetryAfter { get; }

        public static Lease AllowedLease(int remainingPermits) => new(true, remainingPermits, default);
        public static Lease Rejected(DateTimeOffset retryAfter) => new(false, 0, retryAfter);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private int _permitLimit = 60;
        private TimeSpan _window = TimeSpan.FromMinutes(1);
        private Func<DateTimeOffset> _clock = UtcNow;
        private IEqualityComparer<string> _keyComparer = StringComparer.Ordinal;

        internal Builder(string name) => _name = name;

        public Builder WithPermitLimit(int permitLimit)
        {
            _permitLimit = permitLimit;
            return this;
        }

        public Builder WithWindow(TimeSpan window)
        {
            _window = window;
            return this;
        }

        public Builder WithClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public Builder WithKeyComparer(IEqualityComparer<string> keyComparer)
        {
            _keyComparer = keyComparer ?? throw new ArgumentNullException(nameof(keyComparer));
            return this;
        }

        public RateLimitPolicy<TResult> Build()
            => new(_name, _permitLimit, _window, _clock, _keyComparer);
    }
}
