using PatternKit.Cloud.CacheAside;

namespace PatternKit.Cloud.ReadWriteThroughCache;

public sealed class ReadWriteThroughCacheResult<TResult>
{
    public ReadWriteThroughCacheResult(string key, TResult? value, bool found, bool cacheHit, bool written)
    {
        Key = key;
        Value = value;
        Found = found;
        CacheHit = cacheHit;
        Written = written;
    }

    public string Key { get; }
    public TResult? Value { get; }
    public bool Found { get; }
    public bool CacheHit { get; }
    public bool CacheMiss => !CacheHit;
    public bool Written { get; }

    public static ReadWriteThroughCacheResult<TResult> Hit(string key, TResult value)
        => new(key, value, found: true, cacheHit: true, written: false);

    public static ReadWriteThroughCacheResult<TResult> Miss(string key, TResult? value, bool found)
        => new(key, value, found, cacheHit: false, written: false);

    public static ReadWriteThroughCacheResult<TResult> Write(string key, TResult value)
        => new(key, value, found: true, cacheHit: false, written: true);
}

public sealed class ReadWriteThroughCachePolicy<TResult>
{
    private readonly ICacheAsideStore<TResult> _store;
    private readonly TimeSpan? _ttl;

    private ReadWriteThroughCachePolicy(string name, ICacheAsideStore<TResult> store, TimeSpan? ttl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Read/write-through cache policy name is required.", nameof(name));
        if (ttl is not null && ttl.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "Read/write-through cache TTL cannot be negative.");

        Name = name;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _ttl = ttl;
    }

    public string Name { get; }
    public TimeSpan? TimeToLive => _ttl;
    public ICacheAsideStore<TResult> Store => _store;

    public static Builder Create(string name = "read-write-through-cache") => new(name);

    public async ValueTask<ReadWriteThroughCacheResult<TResult>> ReadThroughAsync(
        string key,
        Func<CancellationToken, ValueTask<TResult?>> loader,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (loader is null)
            throw new ArgumentNullException(nameof(loader));

        cancellationToken.ThrowIfCancellationRequested();
        if (_store.TryGet(key, out var cached))
            return ReadWriteThroughCacheResult<TResult>.Hit(key, cached!);

        var value = await loader(cancellationToken).ConfigureAwait(false);
        if (value is null)
            return ReadWriteThroughCacheResult<TResult>.Miss(key, default, found: false);

        _store.Set(key, value, _ttl);
        return ReadWriteThroughCacheResult<TResult>.Miss(key, value, found: true);
    }

    public async ValueTask<ReadWriteThroughCacheResult<TResult>> WriteThroughAsync(
        string key,
        TResult value,
        Func<TResult, CancellationToken, ValueTask> writer,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (writer is null)
            throw new ArgumentNullException(nameof(writer));

        cancellationToken.ThrowIfCancellationRequested();
        await writer(value, cancellationToken).ConfigureAwait(false);
        _store.Set(key, value, _ttl);
        return ReadWriteThroughCacheResult<TResult>.Write(key, value);
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

        public ReadWriteThroughCachePolicy<TResult> Build()
            => new(_name, _store ?? new InMemoryCacheAsideStore<TResult>(), _ttl);
    }
}
