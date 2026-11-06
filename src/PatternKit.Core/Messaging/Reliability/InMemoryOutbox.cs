namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Thread-safe in-memory outbox for tests, demos, and single-process applications.
/// </summary>
public sealed class InMemoryOutbox<TPayload>
{
    private readonly object _gate = new();
    private readonly List<OutboxMessage<TPayload>> _records = new();

    /// <summary>The current outbox records.</summary>
    public IReadOnlyList<OutboxMessage<TPayload>> Records
    {
        get
        {
            lock (_gate)
                return _records.ToArray();
        }
    }

    /// <summary>Adds a message to the outbox.</summary>
    public ValueTask<OutboxMessage<TPayload>> EnqueueAsync(
        Message<TPayload> message,
        string? id = null,
        DateTimeOffset? createdAt = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        cancellationToken.ThrowIfCancellationRequested();

        var record = new OutboxMessage<TPayload>(
            string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!,
            message,
            createdAt ?? DateTimeOffset.UtcNow);

        lock (_gate)
            _records.Add(record);

        return new ValueTask<OutboxMessage<TPayload>>(record);
    }

    /// <summary>Dispatches pending records through <paramref name="dispatcher"/>.</summary>
    public async ValueTask<int> DispatchPendingAsync(
        IOutboxDispatcher<TPayload> dispatcher,
        CancellationToken cancellationToken = default)
    {
        if (dispatcher is null)
            throw new ArgumentNullException(nameof(dispatcher));

        var dispatched = 0;
        foreach (var record in SnapshotPending())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
                Replace(record.Id, record.MarkDispatched(DateTimeOffset.UtcNow));
                dispatched++;
            }
            catch (Exception exception)
            {
                Replace(record.Id, record.WithAttempt(exception.Message));
                throw;
            }
        }

        return dispatched;
    }

    private OutboxMessage<TPayload>[] SnapshotPending()
    {
        lock (_gate)
            return _records.Where(record => !record.Dispatched).ToArray();
    }

    private void Replace(string id, OutboxMessage<TPayload> replacement)
    {
        lock (_gate)
        {
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == id)
                {
                    _records[i] = replacement;
                    return;
                }
            }
        }
    }
}
