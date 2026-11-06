namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Thread-safe in-memory idempotency store for tests, demos, and single-process applications.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>The number of keys stored.</summary>
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
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
                return new ValueTask<IdempotencyClaim>(IdempotencyClaim.Existing(key, entry.Status, entry.Result, entry.FailureReason));

            _entries[key] = new Entry(IdempotencyEntryStatus.Processing, null, null);
            return new ValueTask<IdempotencyClaim>(IdempotencyClaim.ClaimedKey(key));
        }
    }

    /// <inheritdoc />
    public ValueTask MarkCompletedAsync(string key, object? result = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
            _entries[key] = new Entry(IdempotencyEntryStatus.Completed, result, null);

        return default;
    }

    /// <inheritdoc />
    public ValueTask MarkFailedAsync(string key, string? reason = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
            _entries[key] = new Entry(IdempotencyEntryStatus.Failed, null, reason);

        return default;
    }

    /// <summary>Attempts to read the current stored claim for a key.</summary>
    public bool TryGet(string key, out IdempotencyClaim? claim)
    {
        ValidateKey(key);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                claim = IdempotencyClaim.Existing(key, entry.Status, entry.Result, entry.FailureReason);
                return true;
            }
        }

        claim = null;
        return false;
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Idempotency key cannot be null, empty, or whitespace.", nameof(key));
    }

    private sealed class Entry
    {
        internal Entry(IdempotencyEntryStatus status, object? result, string? failureReason)
        {
            Status = status;
            Result = result;
            FailureReason = failureReason;
        }

        internal IdempotencyEntryStatus Status { get; }

        internal object? Result { get; }

        internal string? FailureReason { get; }
    }
}
