namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Pluggable backing store for the Transactional Outbox pattern.
/// Implement this interface to provide durable outbox storage (e.g., relational database, file system).
/// </summary>
/// <typeparam name="TPayload">The outbox message payload type.</typeparam>
public interface IOutboxStore<TPayload>
{
    /// <summary>Adds a message to the outbox and returns the stored record.</summary>
    ValueTask<OutboxMessage<TPayload>> EnqueueAsync(
        Message<TPayload> message,
        string? id = null,
        DateTimeOffset? createdAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all messages that have not yet been dispatched.</summary>
    ValueTask<IReadOnlyList<OutboxMessage<TPayload>>> SnapshotPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Marks a message as successfully dispatched.</summary>
    ValueTask MarkDispatchedAsync(string id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken = default);

    /// <summary>Records a failed dispatch attempt for a message.</summary>
    ValueTask MarkFailedAsync(string id, string? error, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IOutboxStore{TPayload}"/> for tests,
/// demos, and single-process applications.
/// </summary>
/// <typeparam name="TPayload">The outbox message payload type.</typeparam>
public sealed class InMemoryOutboxStore<TPayload> : IOutboxStore<TPayload>
{
    private readonly object _gate = new();
    private readonly List<OutboxMessage<TPayload>> _records = new();

    /// <summary>All records currently held in the store.</summary>
    public IReadOnlyList<OutboxMessage<TPayload>> Records
    {
        get
        {
            lock (_gate)
                return _records.ToArray();
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<OutboxMessage<TPayload>>> SnapshotPendingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<OutboxMessage<TPayload>> pending = _records.Where(r => !r.Dispatched).ToArray();
            return new ValueTask<IReadOnlyList<OutboxMessage<TPayload>>>(pending);
        }
    }

    /// <inheritdoc />
    public ValueTask MarkDispatchedAsync(string id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Outbox message id is required.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == id)
                {
                    _records[i] = _records[i].MarkDispatched(dispatchedAt);
                    return default;
                }
            }
        }

        return default;
    }

    /// <inheritdoc />
    public ValueTask MarkFailedAsync(string id, string? error, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Outbox message id is required.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == id)
                {
                    _records[i] = _records[i].WithAttempt(error);
                    return default;
                }
            }
        }

        return default;
    }
}

/// <summary>
/// Pluggable dispatch loop helper for the Transactional Outbox pattern.
/// Combines an <see cref="IOutboxStore{TPayload}"/> with an <see cref="IOutboxDispatcher{TPayload}"/>
/// to provide a reusable relay loop.
/// </summary>
/// <typeparam name="TPayload">The outbox message payload type.</typeparam>
public sealed class OutboxDispatcher<TPayload>
{
    private readonly IOutboxStore<TPayload> _store;
    private readonly IOutboxDispatcher<TPayload> _dispatcher;

    /// <summary>
    /// Creates an outbox dispatcher bound to the given store and dispatcher.
    /// </summary>
    public OutboxDispatcher(IOutboxStore<TPayload> store, IOutboxDispatcher<TPayload> dispatcher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Drains all pending outbox records by dispatching each through the configured dispatcher.
    /// Returns the number of records successfully dispatched.
    /// </summary>
    public async ValueTask<int> DrainAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _store.SnapshotPendingAsync(cancellationToken).ConfigureAwait(false);
        var dispatched = 0;

        foreach (var record in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
                await _store.MarkDispatchedAsync(record.Id, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
                dispatched++;
            }
            catch (Exception ex)
            {
                await _store.MarkFailedAsync(record.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        return dispatched;
    }

    /// <summary>
    /// Continuously drains pending outbox records until <paramref name="cancellationToken"/> is cancelled,
    /// waiting <paramref name="pollInterval"/> between each drain cycle.
    /// </summary>
    public async ValueTask RunAsync(TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        if (pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "Poll interval must be positive.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Individual dispatch errors are recorded; loop continues
            }

            try
            {
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
