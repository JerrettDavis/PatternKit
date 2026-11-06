namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Pluggable idempotency key store used by idempotent receivers and inbox processors.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to claim <paramref name="key"/> for processing.
    /// </summary>
    ValueTask<IdempotencyClaim> TryClaimAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a claimed key as completed and stores an optional replayable result.
    /// </summary>
    ValueTask MarkCompletedAsync(string key, object? result = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a claimed key as failed with an optional reason.
    /// </summary>
    ValueTask MarkFailedAsync(string key, string? reason = null, CancellationToken cancellationToken = default);
}
