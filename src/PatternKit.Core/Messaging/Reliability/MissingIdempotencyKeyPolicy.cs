namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Defines how an idempotent receiver handles messages without an idempotency key.
/// </summary>
public enum MissingIdempotencyKeyPolicy
{
    /// <summary>Rejects messages without an idempotency key.</summary>
    Reject = 0,

    /// <summary>Processes messages without idempotency protection.</summary>
    Process = 1
}
