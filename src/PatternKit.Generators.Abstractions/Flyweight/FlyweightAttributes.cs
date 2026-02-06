using System;

namespace PatternKit.Generators.Flyweight;

/// <summary>
/// Marks a value type or cache host for Flyweight pattern code generation.
/// The generator produces a cache class with Get, optional TryGet, and Clear methods
/// that ensure a single shared instance per key.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class FlyweightAttribute : Attribute
{
    /// <summary>
    /// The type used as the cache key for flyweight lookups.
    /// </summary>
    public Type KeyType { get; }

    /// <summary>
    /// Creates a new FlyweightAttribute with the specified key type.
    /// </summary>
    /// <param name="keyType">The type used as the cache key.</param>
    public FlyweightAttribute(Type keyType)
    {
        KeyType = keyType;
    }

    /// <summary>
    /// Custom name for the generated cache type.
    /// Default is "{TypeName}Cache".
    /// </summary>
    public string? CacheTypeName { get; set; }

    /// <summary>
    /// Maximum number of entries the cache can hold.
    /// 0 means unbounded (default).
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Eviction policy when the cache reaches capacity.
    /// Default is None (no eviction; new entries rejected when full).
    /// </summary>
    public FlyweightEviction Eviction { get; set; } = FlyweightEviction.None;

    /// <summary>
    /// Threading policy for the generated cache.
    /// Default is Locking (uses lock for thread safety).
    /// </summary>
    public FlyweightThreadingPolicy Threading { get; set; } = FlyweightThreadingPolicy.Locking;

    /// <summary>
    /// Whether to generate a TryGet method.
    /// Default is true.
    /// </summary>
    public bool GenerateTryGet { get; set; } = true;
}

/// <summary>
/// Marks a static factory method that creates flyweight instances.
/// The method must have the signature: static TValue Create(TKey key).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FlyweightFactoryAttribute : Attribute
{
}

/// <summary>
/// Eviction policy for the flyweight cache when capacity is reached.
/// </summary>
public enum FlyweightEviction
{
    /// <summary>
    /// No eviction. When capacity is reached, new entries are rejected
    /// and the factory is called each time without caching.
    /// </summary>
    None = 0,

    /// <summary>
    /// Least Recently Used eviction. When capacity is reached,
    /// the least recently accessed entry is evicted to make room.
    /// </summary>
    Lru = 1
}

/// <summary>
/// Threading policy for the generated flyweight cache.
/// </summary>
public enum FlyweightThreadingPolicy
{
    /// <summary>
    /// No synchronization. Suitable for single-threaded scenarios.
    /// Fastest but not thread-safe.
    /// </summary>
    SingleThreadedFast = 0,

    /// <summary>
    /// Uses lock-based synchronization for thread safety.
    /// Good default for most scenarios.
    /// </summary>
    Locking = 1,

    /// <summary>
    /// Uses ConcurrentDictionary for lock-free thread safety.
    /// Best for high-contention read-heavy scenarios.
    /// </summary>
    Concurrent = 2
}
