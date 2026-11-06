namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Result returned when an idempotency key is claimed.
/// </summary>
public sealed class IdempotencyClaim
{
    internal IdempotencyClaim(bool claimed, string key, IdempotencyEntryStatus status, object? result, string? failureReason)
    {
        Claimed = claimed;
        Key = key;
        Status = status;
        Result = result;
        FailureReason = failureReason;
    }

    /// <summary>Gets whether the caller owns the key and should process the message.</summary>
    public bool Claimed { get; }

    /// <summary>The idempotency key.</summary>
    public string Key { get; }

    /// <summary>The existing or newly-created key status.</summary>
    public IdempotencyEntryStatus Status { get; }

    /// <summary>The replayable result stored for a completed key, when available.</summary>
    public object? Result { get; }

    /// <summary>The stored failure reason for a failed key, when available.</summary>
    public string? FailureReason { get; }

    /// <summary>Creates a claimed key result.</summary>
    public static IdempotencyClaim ClaimedKey(string key) => new(true, ValidateKey(key), IdempotencyEntryStatus.Processing, null, null);

    /// <summary>Creates an existing key result.</summary>
    public static IdempotencyClaim Existing(
        string key,
        IdempotencyEntryStatus status,
        object? result = null,
        string? failureReason = null)
        => new(false, ValidateKey(key), status, result, failureReason);

    private static string ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Idempotency key cannot be null, empty, or whitespace.", nameof(key));

        return key;
    }
}
