namespace PatternKit.Generators.Observer;

/// <summary>
/// Controls how thread safety is implemented for observer subscriptions and publishing.
/// </summary>
public enum ObserverThreadingPolicy
{
    /// <summary>
    /// No synchronization. Use when all subscribe/publish calls happen on a single thread.
    /// Offers the best performance but is not thread-safe.
    /// </summary>
    SingleThreadedFast = 0,

    /// <summary>
    /// Uses lock-based synchronization for thread-safe subscribe/publish.
    /// This is the default policy.
    /// </summary>
    Locking = 1,

    /// <summary>
    /// Uses ConcurrentDictionary-based storage for lock-free concurrent subscribe/publish.
    /// Best for high-contention scenarios with many concurrent publishers.
    /// </summary>
    Concurrent = 2
}
