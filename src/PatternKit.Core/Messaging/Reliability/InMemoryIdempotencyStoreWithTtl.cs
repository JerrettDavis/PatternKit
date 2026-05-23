using System.Collections.Concurrent;

namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Extends <see cref="IIdempotencyStore"/> with optional per-key TTL and periodic eviction.
/// </summary>
public interface IIdempotencyStoreWithTtl : IIdempotencyStore
{
    /// <summary>
    /// Attempts to claim <paramref name="key"/> for processing with an optional time-to-live.
    /// Keys expire after <paramref name="ttl"/> elapses from their creation time.
    /// </summary>
    ValueTask<IdempotencyClaim> TryClaimAsync(string key, TimeSpan? ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts all keys whose TTL has elapsed.
    /// </summary>
    ValueTask<int> EvictExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe in-memory idempotency store with optional per-key TTL and periodic eviction.
/// </summary>
public sealed class InMemoryIdempotencyStoreWithTtl : IIdempotencyStoreWithTtl
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>The number of keys currently stored (including potentially expired ones).</summary>
    public int Count
    {
        get
        {
            lock (_gate)
                return _entries.Count;
        }
    }

    /// <inheritdoc />
    public ValueTask<IdempotencyClaim> TryClaimAsync(string key, CancellationToken cancellationToken = default)
        => TryClaimAsync(key, null, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IdempotencyClaim> TryClaimAsync(string key, TimeSpan? ttl, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                // Treat expired entries as if they don't exist
                if (existing.ExpiresAt.HasValue && existing.ExpiresAt.Value <= now)
                    _entries.Remove(key);
                else
                    return new ValueTask<IdempotencyClaim>(IdempotencyClaim.Existing(key, existing.Status, existing.Result, existing.FailureReason));
            }

            var expiresAt = ttl.HasValue ? now + ttl.Value : (DateTimeOffset?)null;
            _entries[key] = new Entry(IdempotencyEntryStatus.Processing, null, null, expiresAt);
            return new ValueTask<IdempotencyClaim>(IdempotencyClaim.ClaimedKey(key));
        }
    }

    /// <inheritdoc />
    public ValueTask MarkCompletedAsync(string key, object? result = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var expiresAt = _entries.TryGetValue(key, out var existing) ? existing.ExpiresAt : null;
            _entries[key] = new Entry(IdempotencyEntryStatus.Completed, result, null, expiresAt);
        }

        return default;
    }

    /// <inheritdoc />
    public ValueTask MarkFailedAsync(string key, string? reason = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var expiresAt = _entries.TryGetValue(key, out var existing) ? existing.ExpiresAt : null;
            _entries[key] = new Entry(IdempotencyEntryStatus.Failed, null, reason, expiresAt);
        }

        return default;
    }

    /// <inheritdoc />
    public ValueTask<int> EvictExpiredAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var evicted = 0;

        lock (_gate)
        {
            var expiredKeys = new List<string>();
            foreach (var pair in _entries)
            {
                if (pair.Value.ExpiresAt.HasValue && pair.Value.ExpiresAt.Value <= now)
                    expiredKeys.Add(pair.Key);
            }

            foreach (var key in expiredKeys)
            {
                _entries.Remove(key);
                evicted++;
            }
        }

        return new ValueTask<int>(evicted);
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Idempotency key cannot be null, empty, or whitespace.", nameof(key));
    }

    private sealed class Entry
    {
        internal Entry(IdempotencyEntryStatus status, object? result, string? failureReason, DateTimeOffset? expiresAt)
        {
            Status = status;
            Result = result;
            FailureReason = failureReason;
            ExpiresAt = expiresAt;
        }

        internal IdempotencyEntryStatus Status { get; }
        internal object? Result { get; }
        internal string? FailureReason { get; }
        internal DateTimeOffset? ExpiresAt { get; }
    }
}
