namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Dispatches outbox records to an application-owned transport, queue, or handler.
/// </summary>
public interface IOutboxDispatcher<TPayload>
{
    /// <summary>Dispatches a single outbox record.</summary>
    ValueTask DispatchAsync(OutboxMessage<TPayload> message, CancellationToken cancellationToken = default);
}
