namespace PatternKit.Messaging.Reliability;

/// <summary>
/// In-process outbox record that can be persisted by application code before dispatch.
/// </summary>
/// <typeparam name="TPayload">The payload type stored in the outbox record.</typeparam>
public sealed class OutboxMessage<TPayload>
{
    /// <summary>Creates an outbox record.</summary>
    public OutboxMessage(string id, Message<TPayload> message, DateTimeOffset createdAt)
        : this(id, message, createdAt, null, 0, null)
    {
    }

    private OutboxMessage(
        string id,
        Message<TPayload> message,
        DateTimeOffset createdAt,
        DateTimeOffset? dispatchedAt,
        int attempts,
        string? lastError)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Outbox message id cannot be null, empty, or whitespace.", nameof(id));

        Id = id;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        CreatedAt = createdAt;
        DispatchedAt = dispatchedAt;
        Attempts = attempts;
        LastError = lastError;
    }

    /// <summary>The outbox record identifier.</summary>
    public string Id { get; }

    /// <summary>The message to dispatch.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>When the outbox record was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When the outbox record was dispatched, if it has been dispatched.</summary>
    public DateTimeOffset? DispatchedAt { get; }

    /// <summary>The number of dispatch attempts.</summary>
    public int Attempts { get; }

    /// <summary>The last dispatch error, when available.</summary>
    public string? LastError { get; }

    /// <summary>Gets whether the outbox record has been dispatched.</summary>
    public bool Dispatched => DispatchedAt is not null;

    /// <summary>Returns a copy with dispatch attempt metadata recorded.</summary>
    public OutboxMessage<TPayload> WithAttempt(string? error)
        => new(Id, Message, CreatedAt, DispatchedAt, Attempts + 1, error);

    /// <summary>Returns a copy marked as dispatched.</summary>
    public OutboxMessage<TPayload> MarkDispatched(DateTimeOffset dispatchedAt)
        => new(Id, Message, CreatedAt, dispatchedAt, Attempts + 1, null);
}
