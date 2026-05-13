namespace PatternKit.Messaging.Routing;

/// <summary>
/// Result returned after adding a message to an aggregator.
/// </summary>
public sealed class AggregationResult<TKey, TResult>
    where TKey : notnull
{
    private AggregationResult(TKey key, int count, bool accepted, bool completed, TResult? result)
        => (Key, Count, Accepted, Completed, Result) = (key, count, accepted, completed, result);

    /// <summary>The aggregation key.</summary>
    public TKey Key { get; }

    /// <summary>Number of messages currently in the group snapshot.</summary>
    public int Count { get; }

    /// <summary><see langword="true"/> when the added message was accepted into the group.</summary>
    public bool Accepted { get; }

    /// <summary><see langword="true"/> when this add completed the group.</summary>
    public bool Completed { get; }

    /// <summary>The completed result, or <see langword="default"/> while pending.</summary>
    public TResult? Result { get; }

    internal static AggregationResult<TKey, TResult> Pending(TKey key, int count, bool accepted)
        => new(key, count, accepted, completed: false, result: default);

    internal static AggregationResult<TKey, TResult> Complete(TKey key, int count, bool accepted, TResult result)
        => new(key, count, accepted, completed: true, result);
}
