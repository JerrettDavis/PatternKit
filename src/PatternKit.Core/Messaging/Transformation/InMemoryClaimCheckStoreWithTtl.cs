using System.Collections.Concurrent;

namespace PatternKit.Messaging.Transformation;

/// <summary>
/// Extends <see cref="IClaimCheckStore{TPayload}"/> with optional per-entry TTL and eviction.
/// </summary>
/// <typeparam name="TPayload">The payload type stored in the claim check.</typeparam>
public interface IClaimCheckStoreWithTtl<TPayload> : IClaimCheckStore<TPayload>
{
    /// <summary>
    /// Stores <paramref name="payload"/> under <paramref name="claimId"/>, optionally expiring after <paramref name="ttl"/>.
    /// </summary>
    ValueTask StoreAsync(
        string claimId,
        TPayload payload,
        MessageHeaders headers,
        TimeSpan? ttl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evicts all entries whose TTL has elapsed.
    /// Returns the number of entries removed.
    /// </summary>
    ValueTask<int> EvictExpiredAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe in-memory claim check store with optional per-entry TTL and eviction.
/// Expired entries are removed lazily on read and proactively via <see cref="EvictExpiredAsync"/>.
/// </summary>
/// <typeparam name="TPayload">The payload type.</typeparam>
public sealed class InMemoryClaimCheckStoreWithTtl<TPayload> : IClaimCheckStoreWithTtl<TPayload>
{
    private readonly ConcurrentDictionary<string, TimedEntry> _items = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask StoreAsync(string claimId, TPayload payload, MessageHeaders headers, CancellationToken cancellationToken = default)
        => StoreAsync(claimId, payload, headers, null, cancellationToken);

    /// <inheritdoc />
    public ValueTask StoreAsync(
        string claimId,
        TPayload payload,
        MessageHeaders headers,
        TimeSpan? ttl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(claimId))
            throw new ArgumentException("Claim id is required.", nameof(claimId));
        if (headers is null)
            throw new ArgumentNullException(nameof(headers));
        if (ttl.HasValue && ttl.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must not be negative.");

        var expiresAt = ttl.HasValue ? DateTimeOffset.UtcNow + ttl.Value : (DateTimeOffset?)null;
        _items[claimId] = new TimedEntry(new ClaimCheckStoredPayload<TPayload>(payload, headers), expiresAt);
        return default;
    }

    /// <inheritdoc />
    public ValueTask<ClaimCheckStoredPayload<TPayload>?> TryLoadAsync(string claimId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(claimId))
            throw new ArgumentException("Claim id is required.", nameof(claimId));

        if (!_items.TryGetValue(claimId, out var entry))
            return new ValueTask<ClaimCheckStoredPayload<TPayload>?>(result: null);

        // Lazy expiry check
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            _items.TryRemove(claimId, out _);
            return new ValueTask<ClaimCheckStoredPayload<TPayload>?>(result: null);
        }

        return new ValueTask<ClaimCheckStoredPayload<TPayload>?>(entry.Payload);
    }

    /// <inheritdoc />
    public ValueTask<int> EvictExpiredAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var evicted = 0;

        foreach (var pair in _items)
        {
            if (pair.Value.ExpiresAt.HasValue && pair.Value.ExpiresAt.Value <= now)
            {
                if (_items.TryRemove(pair.Key, out _))
                    evicted++;
            }
        }

        return new ValueTask<int>(evicted);
    }

    private sealed class TimedEntry
    {
        internal TimedEntry(ClaimCheckStoredPayload<TPayload> payload, DateTimeOffset? expiresAt)
            => (Payload, ExpiresAt) = (payload, expiresAt);

        internal ClaimCheckStoredPayload<TPayload> Payload { get; }
        internal DateTimeOffset? ExpiresAt { get; }
    }
}
