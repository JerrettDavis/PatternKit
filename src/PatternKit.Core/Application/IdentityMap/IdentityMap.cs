namespace PatternKit.Application.IdentityMap;

/// <summary>Request or unit-of-work scoped cache that preserves object identity by key.</summary>
public interface IIdentityMap<TEntity, TKey>
    where TKey : notnull
{
    int Count { get; }

    TEntity? Get(TKey key);

    IdentityMapResult<TEntity> Track(TKey key, TEntity entity);

    TEntity GetOrAdd(TKey key, Func<TKey, TEntity> factory);

    bool Remove(TKey key);

    void Clear();
}

/// <summary>In-memory Identity Map for preserving object identity in a scope.</summary>
public sealed class IdentityMap<TEntity, TKey> : IIdentityMap<TEntity, TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TEntity> _entities;
    private readonly Func<TEntity, TKey>? _keySelector;

    private IdentityMap(Func<TEntity, TKey>? keySelector, IEqualityComparer<TKey>? comparer)
    {
        _keySelector = keySelector;
        _entities = new Dictionary<TKey, TEntity>(comparer);
    }

    public int Count => _entities.Count;

    public static Builder Create(Func<TEntity, TKey>? keySelector = null)
        => new(keySelector);

    public TEntity? Get(TKey key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        _entities.TryGetValue(key, out var entity);
        return entity;
    }

    public IdentityMapResult<TEntity> Track(TEntity entity)
    {
        if (_keySelector is null)
            throw new InvalidOperationException("A key selector is required to track entities without an explicit key.");

        return Track(_keySelector(entity), entity);
    }

    public IdentityMapResult<TEntity> Track(TKey key, TEntity entity)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (entity is null)
            throw new ArgumentNullException(nameof(entity));

        if (_entities.TryGetValue(key, out var existing))
        {
            return ReferenceEquals(existing, entity)
                ? IdentityMapResult<TEntity>.Existing(existing)
                : IdentityMapResult<TEntity>.Conflict(existing, "A different entity instance is already tracked for this key.");
        }

        _entities.Add(key, entity);
        return IdentityMapResult<TEntity>.Tracked(entity);
    }

    public TEntity GetOrAdd(TKey key, Func<TKey, TEntity> factory)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        if (_entities.TryGetValue(key, out var existing))
            return existing;

        var created = factory(key);
        _entities.Add(key, created);
        return created;
    }

    public bool Remove(TKey key)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        return _entities.Remove(key);
    }

    public void Clear() => _entities.Clear();

    public sealed class Builder
    {
        private readonly Func<TEntity, TKey>? _keySelector;
        private IEqualityComparer<TKey>? _comparer;

        internal Builder(Func<TEntity, TKey>? keySelector)
        {
            _keySelector = keySelector;
        }

        public Builder UseComparer(IEqualityComparer<TKey> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        public IdentityMap<TEntity, TKey> Build()
            => new(_keySelector, _comparer);
    }
}

/// <summary>Result returned when tracking an entity in an Identity Map.</summary>
public sealed class IdentityMapResult<TEntity>
{
    private IdentityMapResult(TEntity entity, IdentityMapStatus status, string? reason)
    {
        Entity = entity;
        Status = status;
        Reason = reason;
    }

    public TEntity Entity { get; }

    public IdentityMapStatus Status { get; }

    public string? Reason { get; }

    public bool Succeeded => Status is IdentityMapStatus.Tracked or IdentityMapStatus.Existing;

    public static IdentityMapResult<TEntity> Tracked(TEntity entity)
        => new(entity, IdentityMapStatus.Tracked, null);

    public static IdentityMapResult<TEntity> Existing(TEntity entity)
        => new(entity, IdentityMapStatus.Existing, null);

    public static IdentityMapResult<TEntity> Conflict(TEntity entity, string reason)
        => new(entity, IdentityMapStatus.Conflict, string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Identity Map conflict reason is required.", nameof(reason))
            : reason);
}

public enum IdentityMapStatus
{
    Tracked,
    Existing,
    Conflict
}
