namespace PatternKit.Generators.Observer;

/// <summary>
/// Defines the threading policy for an observer pattern implementation.
/// </summary>
public enum ObserverThreadingPolicy
{
    /// <summary>
    /// No thread safety. Fast, but not safe for concurrent Subscribe/Unsubscribe/Publish.
    /// Use only when all operations occur on a single thread.
    /// </summary>
    SingleThreadedFast = 0,

    /// <summary>
    /// Uses locking for thread safety. Subscribe/Unsubscribe operations take locks,
    /// and Publish snapshots the subscriber list under a lock for deterministic iteration.
    /// Default and recommended for most scenarios.
    /// </summary>
    Locking = 1,

    /// <summary>
    /// Lock-free concurrent implementation using atomic operations.
    /// Thread-safe with potentially better performance under high concurrency,
    /// but ordering may degrade to Undefined unless additional work is done.
    /// </summary>
    Concurrent = 2
}
