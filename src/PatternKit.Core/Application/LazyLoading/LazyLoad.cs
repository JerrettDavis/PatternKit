namespace PatternKit.Application.LazyLoading;

public sealed class LazyLoadResult<TValue>
{
    public LazyLoadResult(TValue value, bool loaded, bool cached, DateTimeOffset loadedAt)
    {
        Value = value;
        Loaded = loaded;
        Cached = cached;
        LoadedAt = loadedAt;
    }

    public TValue Value { get; }
    public bool Loaded { get; }
    public bool Cached { get; }
    public DateTimeOffset LoadedAt { get; }
}

public sealed class LazyLoad<TValue>
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<CancellationToken, ValueTask<TValue>> _loader;
    private readonly Func<DateTimeOffset> _utcNow;
    private LazyLoadResult<TValue>? _current;
    private int _version;

    private LazyLoad(string name, Func<CancellationToken, ValueTask<TValue>> loader, bool cacheEnabled, TimeSpan? timeToLive, Func<DateTimeOffset> utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Lazy load name is required.", nameof(name));

        Name = name;
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        CacheEnabled = cacheEnabled;
        TimeToLive = timeToLive;
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public string Name { get; }
    public bool CacheEnabled { get; }
    public TimeSpan? TimeToLive { get; }
    public bool IsLoaded => TryGetCurrent(out _);

    public static Builder Create(string name = "lazy-load") => new(name);

    public async ValueTask<LazyLoadResult<TValue>> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryGetCurrent(out var current))
            return new(current.Value, false, true, current.LoadedAt);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetCurrent(out current))
                return new(current.Value, false, true, current.LoadedAt);

            var version = Volatile.Read(ref _version);
            var value = await _loader(cancellationToken).ConfigureAwait(false);
            var loaded = new LazyLoadResult<TValue>(value, true, false, _utcNow());
            if (CacheEnabled && version == Volatile.Read(ref _version))
                Volatile.Write(ref _current, loaded);

            return loaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _version);
        Volatile.Write(ref _current, null);
    }

    private bool TryGetCurrent(out LazyLoadResult<TValue> current)
    {
        var candidate = Volatile.Read(ref _current);
        if (candidate is not null && !IsExpired(candidate))
        {
            current = candidate;
            return true;
        }

        current = null!;
        return false;
    }

    private bool IsExpired(LazyLoadResult<TValue> current)
        => TimeToLive is { } ttl && _utcNow() - current.LoadedAt >= ttl;

    public sealed class Builder
    {
        private readonly string _name;
        private Func<CancellationToken, ValueTask<TValue>>? _loader;
        private bool _cacheEnabled = true;
        private TimeSpan? _timeToLive;
        private Func<DateTimeOffset> _utcNow = () => DateTimeOffset.UtcNow;

        internal Builder(string name) => _name = name;

        public Builder LoadWith(Func<CancellationToken, ValueTask<TValue>> loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            return this;
        }

        public Builder DisableCache()
        {
            _cacheEnabled = false;
            return this;
        }

        public Builder WithTimeToLive(TimeSpan timeToLive)
        {
            if (timeToLive <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeToLive), timeToLive, "Lazy load time to live must be positive.");

            _timeToLive = timeToLive;
            return this;
        }

        public Builder WithClock(Func<DateTimeOffset> utcNow)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            return this;
        }

        public LazyLoad<TValue> Build()
        {
            if (_loader is null)
                throw new InvalidOperationException("Lazy load requires a loader.");

            return new(_name, _loader, _cacheEnabled, _timeToLive, _utcNow);
        }
    }
}
