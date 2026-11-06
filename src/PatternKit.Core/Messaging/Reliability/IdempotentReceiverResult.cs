namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Result returned by an idempotent receiver.
/// </summary>
/// <typeparam name="TResult">The handler result type.</typeparam>
public sealed class IdempotentReceiverResult<TResult>
{
    internal IdempotentReceiverResult(IdempotentReceiverStatus status, string? key, TResult? result)
    {
        Status = status;
        Key = key;
        Result = result;
    }

    /// <summary>The receiver outcome.</summary>
    public IdempotentReceiverStatus Status { get; }

    /// <summary>The idempotency key used for the invocation, when available.</summary>
    public string? Key { get; }

    /// <summary>The processed or replayed handler result, when available.</summary>
    public TResult? Result { get; }

    /// <summary>Gets whether the handler ran for this invocation.</summary>
    public bool Processed => Status == IdempotentReceiverStatus.Processed;

    /// <summary>Creates a processed result.</summary>
    public static IdempotentReceiverResult<TResult> ProcessedResult(string? key, TResult result)
        => new(IdempotentReceiverStatus.Processed, key, result);

    /// <summary>Creates a duplicate result.</summary>
    public static IdempotentReceiverResult<TResult> Duplicate(string key)
        => new(IdempotentReceiverStatus.Duplicate, key, default);

    /// <summary>Creates a replayed result.</summary>
    public static IdempotentReceiverResult<TResult> Replayed(string key, TResult result)
        => new(IdempotentReceiverStatus.Replayed, key, result);

    /// <summary>Creates a missing-key result.</summary>
    public static IdempotentReceiverResult<TResult> MissingKey()
        => new(IdempotentReceiverStatus.MissingKey, null, default);
}
