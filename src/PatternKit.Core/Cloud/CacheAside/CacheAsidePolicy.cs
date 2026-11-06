namespace PatternKit.Cloud.CacheAside;

/// <summary>
/// Outcome returned by a cache-aside policy lookup.
/// </summary>
public sealed class CacheAsideResult<TResult>
{
    public CacheAsideResult(TResult? value, bool found, bool cacheHit, string key)
    {
        Value = value;
        Found = found;
        CacheHit = cacheHit;
        Key = key;
    }

    public TResult? Value { get; }
    public bool Found { get; }
    public bool CacheHit { get; }
    public bool CacheMiss => !CacheHit;
    public string Key { get; }

    public static CacheAsideResult<TResult> Hit(string key, TResult value)
        => new(value, true, true, key);

    public static CacheAsideResult<TResult> Miss(string key, TResult? value, bool found)
        => new(value, found, false, key);
}

/// <summary>
/// Store abstraction used by cache-aside policies.
/// </summary>
public interface ICacheAsideStore<TResult>
{
    bool TryGet(string key, out TResult? value);
    void Set(string key, TResult value, TimeSpan? ttl);
    bool Remove(string key);
    void Clear();
}

/// <summary>
/// In-memory cache-aside store suitable for examples, tests, and process-local caches.
/// </summary>
public sealed class InMemoryCacheAsideStore<TResult> : ICacheAsideStore<TResult>
{
    private readonly object _gate = new();
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    public InMemoryCacheAsideStore(Func<DateTimeOffset>? clock = null)
        => _clock = clock ?? UtcNow;

    public bool TryGet(string key, out TResult? value)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                value = default;
                return false;
            }

            if (entry.ExpiresAt is not null && entry.ExpiresAt.Value <= _clock())
            {
                _entries.Remove(key);
                value = default;
                return false;
            }

            value = entry.Value;
            return true;
        }
    }

    public void Set(string key, TResult value, TimeSpan? ttl)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (ttl is not null && ttl.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "Cache entry TTL cannot be negative.");

        lock (_gate)
        {
            DateTimeOffset? expiresAt = ttl is null ? null : _clock().Add(ttl.Value);
            _entries[key] = new CacheEntry(value, expiresAt);
        }
    }

    public bool Remove(string key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        lock (_gate)
            return _entries.Remove(key);
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();
    }

    private sealed class CacheEntry
    {
        public CacheEntry(TResult value, DateTimeOffset? expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
        }

        public TResult Value { get; }
        public DateTimeOffset? ExpiresAt { get; }
    }

    private static DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}

/// <summary>
/// Cache-aside policy that reads through a cache and populates misses from an origin loader.
/// </summary>
public sealed class CacheAsidePolicy<TResult>
{
    public delegate bool CachePredicate(TResult value);

    private readonly ICacheAsideStore<TResult> _store;
    private readonly TimeSpan? _ttl;
    private readonly CachePredicate _shouldCache;

    private CacheAsidePolicy(string name, ICacheAsideStore<TResult> store, TimeSpan? ttl, CachePredicate shouldCache)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Cache-aside policy name is required.", nameof(name));
        if (ttl is not null && ttl.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "Cache-aside TTL cannot be negative.");

        Name = name;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _ttl = ttl;
        _shouldCache = shouldCache ?? throw new ArgumentNullException(nameof(shouldCache));
    }

    public string Name { get; }
    public TimeSpan? TimeToLive => _ttl;
    public ICacheAsideStore<TResult> Store => _store;

    public static Builder Create(string name = "cache-aside") => new(name);

    public CacheAsideResult<TResult> GetOrLoad(string key, Func<TResult?> loader)
    {
        ValidateKey(key);
        if (loader is null)
            throw new ArgumentNullException(nameof(loader));

        if (_store.TryGet(key, out var cached))
            return CacheAsideResult<TResult>.Hit(key, cached!);

        var value = loader();
        if (value is null)
            return CacheAsideResult<TResult>.Miss(key, default, found: false);

        if (_shouldCache(value))
            _store.Set(key, value, _ttl);

        return CacheAsideResult<TResult>.Miss(key, value, found: true);
    }

    public async ValueTask<CacheAsideResult<TResult>> GetOrLoadAsync(
        string key,
        Func<CancellationToken, ValueTask<TResult?>> loader,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (loader is null)
            throw new ArgumentNullException(nameof(loader));

        cancellationToken.ThrowIfCancellationRequested();
        if (_store.TryGet(key, out var cached))
            return CacheAsideResult<TResult>.Hit(key, cached!);

        var value = await loader(cancellationToken).ConfigureAwait(false);
        if (value is null)
            return CacheAsideResult<TResult>.Miss(key, default, found: false);

        if (_shouldCache(value))
            _store.Set(key, value, _ttl);

        return CacheAsideResult<TResult>.Miss(key, value, found: true);
    }

    public bool Invalidate(string key)
    {
        ValidateKey(key);
        return _store.Remove(key);
    }

    public void Clear() => _store.Clear();

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key is required.", nameof(key));
    }

    public sealed class Builder
    {
        private readonly string _name;
        private ICacheAsideStore<TResult>? _store;
        private TimeSpan? _ttl;
        private CachePredicate _shouldCache = static _ => true;

        internal Builder(string name) => _name = name;

        public Builder WithStore(ICacheAsideStore<TResult> store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        public Builder WithTimeToLive(TimeSpan ttl)
        {
            _ttl = ttl;
            return this;
        }

        public Builder WithoutExpiration()
        {
            _ttl = null;
            return this;
        }

        public Builder CacheWhen(CachePredicate predicate)
        {
            _shouldCache = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public CacheAsidePolicy<TResult> Build()
            => new(_name, _store ?? new InMemoryCacheAsideStore<TResult>(), _ttl, _shouldCache);
    }
}
