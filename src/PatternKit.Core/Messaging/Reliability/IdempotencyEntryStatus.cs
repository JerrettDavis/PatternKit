namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Describes the stored state of an idempotency key.
/// </summary>
public enum IdempotencyEntryStatus
{
    /// <summary>The key has been claimed and is currently processing.</summary>
    Processing = 0,

    /// <summary>The key completed successfully and may have a replayable result.</summary>
    Completed = 1,

    /// <summary>The key failed. Receivers may choose whether later attempts can retry.</summary>
    Failed = 2
}
