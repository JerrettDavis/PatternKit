namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Describes the outcome of an idempotent receiver invocation.
/// </summary>
public enum IdempotentReceiverStatus
{
    /// <summary>The message was processed by the handler.</summary>
    Processed = 0,

    /// <summary>The message was a duplicate and was suppressed.</summary>
    Duplicate = 1,

    /// <summary>The message was a duplicate and a stored completed result was replayed.</summary>
    Replayed = 2,

    /// <summary>The message had no idempotency key and was rejected.</summary>
    MissingKey = 3
}
