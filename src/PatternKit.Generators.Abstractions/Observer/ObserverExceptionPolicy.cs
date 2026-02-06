namespace PatternKit.Generators.Observer;

/// <summary>
/// Controls how exceptions thrown by subscribers are handled during publish.
/// </summary>
public enum ObserverExceptionPolicy
{
    /// <summary>
    /// Stop publishing on first exception and rethrow immediately.
    /// </summary>
    Stop = 0,

    /// <summary>
    /// Continue publishing to remaining subscribers after an exception.
    /// The first exception is rethrown after all subscribers have been notified.
    /// This is the default policy.
    /// </summary>
    Continue = 1,

    /// <summary>
    /// Continue publishing to all subscribers and aggregate all exceptions
    /// into an <see cref="System.AggregateException"/> thrown after completion.
    /// </summary>
    Aggregate = 2
}
