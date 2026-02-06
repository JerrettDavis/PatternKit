using System.Runtime.CompilerServices;

namespace PatternKit.Structural.Flyweight;

/// <summary>
/// Allocation-light, thread-safe flyweight cache that shares immutable/intrinsic objects by key.
/// Build once via the <see cref="Builder"/> then call <see cref="Get"/> to obtain (or lazily create)
/// a shared instance for a given key. Instances are created at most once per key and reused thereafter.
/// </summary>
/// <typeparam name="TKey">Key identifying intrinsic state. Must supply stable equality semantics.</typeparam>
/// <typeparam name="TValue">Intrinsic value type to be shared. Should be immutable or thread-safe.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: A Flyweight splits state into <i>intrinsic</i> (shared, invariant, heavy) and
/// <i>extrinsic</i> (context specific, passed in at use time). This implementation focuses on efficiently
/// materializing and caching intrinsic objects keyed by <typeparamref name="TKey"/> while letting callers
/// supply any extrinsic data when they actually use <typeparamref name="TValue"/>.
/// </para>
/// <para>
/// <b>Use cases</b>:
/// <list type="bullet">
///   <item><description>Text rendering (glyph / font style objects)</description></item>
///   <item><description>Game entities (tree / particle / sprite metadata)</description></item>
///   <item><description>AST / parser nodes reused across compilations</description></item>
///   <item><description>Icon / image / brush caches in UI frameworks</description></item>
///   <item><description>Reflection metadata / serialization schema sharing</description></item>
/// </list>
/// </para>
/// <para><b>Thread-safety</b>: All read paths are lock-free after the first creation; creation uses
/// double-checked locking to guarantee a single factory invocation per key.</para>
/// <para><b>Performance</b>: Only 1 allocation for the flyweight plus one <see cref="Dictionary{TKey,TValue}"/>.
/// No per-call allocations. Creation path uses a single lock with fast uncontended exit.</para>
/// <para><b>Immutability</b>: The flyweight itself is immutable after <see cref="Builder.Build"/>; the internal
/// dictionary is only mutated during first-time creation of a key's value.</para>
/// <para>
/// <b>Contrast</b>: Unlike a general-purpose cache, this flyweight only guarantees identity sharing for intrinsic objects.
/// It intentionally omits eviction, expiration, or size policies to stay minimal and allocation-light.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create a style flyweight where the key encodes style aspects.
/// var styles = Flyweight&lt;string, TextStyle&gt;.Create()
///     .WithFactory(key =&gt; ParseStyle(key))
///     .Preload("default", new TextStyle("Consolas", 12, "#EEE", false, false))
///     .Build();
/// var s1 = styles.Get("default");
/// var s2 = styles.Get("default");
/// // s1 and s2 refer to the same shared instance.
/// </code>
/// </example>
public sealed class Flyweight<TKey, TValue> where TKey : notnull
{
    /// <summary>
    /// Factory function creating a new intrinsic value for a key when it is requested the first time.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <returns>New <typeparamref name="TValue"/> to store and share.</returns>
    public delegate TValue Factory(TKey key);

    private readonly Dictionary<TKey, TValue> _cache;
    private readonly Factory _factory;
    private readonly object _lock = new();

    private Flyweight(Dictionary<TKey, TValue> cache, Factory factory)
    {
        _cache = cache;
        _factory = factory;
    }

    /// <summary>
    /// Gets (or lazily creates) the shared intrinsic value for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">Lookup key (passed by <c>in</c> to avoid defensive copies).</param>
    /// <returns>Cached shared instance.</returns>
    /// <remarks>
    /// Uses a double-checked locking pattern:
    /// <list type="number">
    ///   <item><description>Fast path: try a dictionary without lock.</description></item>
    ///   <item><description>Slow path: lock, re-check, then create, and store.</description></item>
    /// </list>
    /// Subsequent calls for the same key take the fast path.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Get(in TKey key)
        => _cache.TryGetValue(key, out var existing) ? existing : AddSlow(key);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private TValue AddSlow(TKey key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existing))
                return existing;
            var created = _factory(key) ?? throw new InvalidOperationException("Factory returned null value.");
            _cache[key] = created;
            return created;
        }
    }

    /// <summary>
    /// Attempts to get the intrinsic value if already created without forcing creation.
    /// </summary>
    /// <param name="key">Lookup key.</param>
    /// <param name="value">Existing value if present.</param>
    /// <returns><see langword="true"/> if already cached; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetExisting(in TKey key, out TValue value) => _cache.TryGetValue(key, out value!);

    /// <summary>
    /// Number of distinct intrinsic values currently cached.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _cache.Count;
    }

    /// <summary>
    /// Enumerates current flyweight entries (snapshot semantics).
    /// </summary>
    public IReadOnlyDictionary<TKey, TValue> Snapshot()
    {
        lock (_lock)
        {
            return new Dictionary<TKey, TValue>(_cache); // small defensive copy
        }
    }

    /// <summary>
    /// Starts a new <see cref="Builder"/>.
    /// </summary>
    public static Builder Create() => new();

    /// <summary>
    /// Fluent builder for <see cref="Flyweight{TKey, TValue}"/>.
    /// </summary>
    /// <remarks>
    /// <para>Configure a required <see cref="WithFactory"/> and optional capacity, comparer, or preloaded items.</para>
    /// <para>Builders are <b>not</b> thread-safe; build once then reuse the flyweight.</para>
    /// </remarks>
    public sealed class Builder
    {
        private Factory? _factory;
        private int _capacity;
        private IEqualityComparer<TKey>? _comparer;
        private List<KeyValuePair<TKey, TValue>>? _preload;

        internal Builder() { }

        /// <summary>
        /// Sets the factory used to create the intrinsic value for a key on first request.
        /// </summary>
        public Builder WithFactory(Factory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        /// <summary>
        /// Sets initial dictionary capacity (hint to reduce rehashing for known key counts).
        /// </summary>
        public Builder WithCapacity(int capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            return this;
        }

        /// <summary>
        /// Sets custom equality comparer for keys.
        /// </summary>
        public Builder WithComparer(IEqualityComparer<TKey> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        /// <summary>
        /// Preloads a key/value pair that is known ahead of time. Repeated calls accumulate.
        /// </summary>
        public Builder Preload(TKey key, TValue value)
        {
            _preload ??= new();
            _preload.Add(new KeyValuePair<TKey, TValue>(key, value));
            return this;
        }

        /// <summary>
        /// Preloads multiple key/value pairs.
        /// </summary>
        public Builder Preload(params (TKey key, TValue value)[] items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));
            if (items.Length == 0) return this;
            _preload ??= new(items.Length);
            foreach (var (k, v) in items) _preload.Add(new KeyValuePair<TKey, TValue>(k, v));
            return this;
        }

        /// <summary>
        /// Builds an immutable flyweight cache instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if required factory not specified.</exception>
        public Flyweight<TKey, TValue> Build()
        {
            if (_factory is null)
                throw new InvalidOperationException("Flyweight requires a factory. Call WithFactory().");

            var capacity = _capacity;
            if (_preload is { Count: > 0 })
            {
                // if user did not specify capacity, scale to preload count
                if (_capacity == 0) capacity = _preload.Count;
            }

            var dict = capacity > 0
                ? new Dictionary<TKey, TValue>(capacity, _comparer)
                : new Dictionary<TKey, TValue>(_comparer);

            if (_preload is not { Count: > 0 })
                return new Flyweight<TKey, TValue>(dict, _factory);


            foreach (var kv in _preload)
            {
                dict[kv.Key] = kv.Value; // last wins
            }

            return new Flyweight<TKey, TValue>(dict, _factory);
        }
    }
}
