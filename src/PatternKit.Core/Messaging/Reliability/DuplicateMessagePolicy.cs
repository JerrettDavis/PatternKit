namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Defines how an idempotent receiver handles a duplicate idempotency key.
/// </summary>
public enum DuplicateMessagePolicy
{
    /// <summary>Suppresses duplicates and returns no handler result.</summary>
    Suppress = 0,

    /// <summary>Replays a stored completed result when it is assignable to the receiver result type.</summary>
    ReplayCompleted = 1
}
