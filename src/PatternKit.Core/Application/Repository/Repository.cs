using PatternKit.Application.Specification;

namespace PatternKit.Application.Repository;

/// <summary>
/// Async collection-like persistence boundary for domain entities.
/// </summary>
public interface IRepository<TEntity, TKey>
    where TKey : notnull
{
    /// <summary>Adds a new entity and rejects duplicate keys.</summary>
    ValueTask<RepositoryResult<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Gets an entity by key.</summary>
    ValueTask<TEntity?> GetAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>Lists all tracked entities.</summary>
    ValueTask<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists entities matching a specification.</summary>
    ValueTask<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>Replaces an existing entity and rejects missing keys.</summary>
    ValueTask<RepositoryResult<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Removes an entity by key.</summary>
    ValueTask<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default);
}

/// <summary>In-memory repository implementation for tests, samples, and embedded applications.</summary>
public sealed class InMemoryRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TEntity> _entities;
    private readonly Func<TEntity, TKey> _keySelector;

    private InMemoryRepository(Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer)
    {
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _entities = new Dictionary<TKey, TEntity>(comparer);
    }

    /// <summary>Creates an in-memory repository builder.</summary>
    public static Builder Create(Func<TEntity, TKey> keySelector)
        => new(keySelector);

    /// <inheritdoc />
    public ValueTask<RepositoryResult<TEntity>> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity));

        cancellationToken.ThrowIfCancellationRequested();
        var key = _keySelector(entity);
        if (_entities.ContainsKey(key))
            return new ValueTask<RepositoryResult<TEntity>>(RepositoryResult<TEntity>.Conflict(entity, $"Entity with key '{key}' already exists."));

        _entities[key] = entity;
        return new ValueTask<RepositoryResult<TEntity>>(RepositoryResult<TEntity>.Stored(entity));
    }

    /// <inheritdoc />
    public ValueTask<TEntity?> GetAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        cancellationToken.ThrowIfCancellationRequested();
        _entities.TryGetValue(key, out var entity);
        return new ValueTask<TEntity?>(entity);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<IReadOnlyList<TEntity>>(_entities.Values.ToArray());
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        if (specification is null)
            throw new ArgumentNullException(nameof(specification));

        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<IReadOnlyList<TEntity>>(_entities.Values.Where(specification.IsSatisfiedBy).ToArray());
    }

    /// <inheritdoc />
    public ValueTask<RepositoryResult<TEntity>> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity));

        cancellationToken.ThrowIfCancellationRequested();
        var key = _keySelector(entity);
        if (!_entities.ContainsKey(key))
            return new ValueTask<RepositoryResult<TEntity>>(RepositoryResult<TEntity>.Missing(entity, $"Entity with key '{key}' was not found."));

        _entities[key] = entity;
        return new ValueTask<RepositoryResult<TEntity>>(RepositoryResult<TEntity>.Stored(entity));
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(TKey key, CancellationToken cancellationToken = default)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));

        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<bool>(_entities.Remove(key));
    }

    /// <summary>Fluent builder for in-memory repositories.</summary>
    public sealed class Builder
    {
        private readonly Func<TEntity, TKey> _keySelector;
        private IEqualityComparer<TKey>? _comparer;

        internal Builder(Func<TEntity, TKey> keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        /// <summary>Uses a custom key comparer.</summary>
        public Builder UseComparer(IEqualityComparer<TKey> comparer)
        {
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        /// <summary>Builds the repository.</summary>
        public InMemoryRepository<TEntity, TKey> Build()
            => new(_keySelector, _comparer);
    }
}

/// <summary>Result returned by repository mutation operations.</summary>
public sealed class RepositoryResult<TEntity>
{
    private RepositoryResult(TEntity entity, RepositoryStatus status, string? reason)
    {
        Entity = entity;
        Status = status;
        Reason = reason;
    }

    /// <summary>The entity supplied to the repository operation.</summary>
    public TEntity Entity { get; }

    /// <summary>Operation status.</summary>
    public RepositoryStatus Status { get; }

    /// <summary>Gets whether the entity was stored.</summary>
    public bool Succeeded => Status == RepositoryStatus.Stored;

    /// <summary>Conflict or missing reason when the operation did not store the entity.</summary>
    public string? Reason { get; }

    /// <summary>Creates a successful mutation result.</summary>
    public static RepositoryResult<TEntity> Stored(TEntity entity)
        => new(entity, RepositoryStatus.Stored, null);

    /// <summary>Creates a duplicate-key result.</summary>
    public static RepositoryResult<TEntity> Conflict(TEntity entity, string reason)
        => new(entity, RepositoryStatus.Conflict, ValidateReason(reason));

    /// <summary>Creates a missing-entity result.</summary>
    public static RepositoryResult<TEntity> Missing(TEntity entity, string reason)
        => new(entity, RepositoryStatus.Missing, ValidateReason(reason));

    private static string ValidateReason(string reason)
        => string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Repository result reason is required.", nameof(reason))
            : reason;
}

/// <summary>Repository mutation status.</summary>
public enum RepositoryStatus
{
    Stored,
    Conflict,
    Missing
}
