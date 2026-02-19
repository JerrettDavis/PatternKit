namespace PatternKit.Generators.Observer;

/// <summary>
/// Defines how exceptions from event handlers are handled during publishing.
/// </summary>
public enum ObserverExceptionPolicy
{
    /// <summary>
    /// Continue invoking all handlers even if some throw exceptions.
    /// Exceptions are either swallowed or routed to an optional error hook.
    /// Default and safest for most scenarios.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Stop publishing and rethrow the first exception encountered.
    /// Remaining handlers are not invoked.
    /// </summary>
    Stop = 1,

    /// <summary>
    /// Invoke all handlers and collect any exceptions.
    /// Throws an AggregateException at the end if any handlers threw.
    /// </summary>
    Aggregate = 2
}
