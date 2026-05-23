namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Convenience extension methods for <see cref="IOutboxStore{TPayload}"/>.
/// </summary>
public static class OutboxStoreExtensions
{
    /// <summary>
    /// Convenience overload that accepts an untyped payload and an optional header dictionary,
    /// boxes them into a <see cref="Message{TPayload}">Message&lt;object&gt;</see>, and enqueues
    /// via the typed store.
    /// </summary>
    /// <remarks>
    /// Use when the calling layer does not have compile-time knowledge of the payload type
    /// (e.g., a generic workflow-step orchestrator). Projects that want this convenience
    /// should consume <see cref="IOutboxStore{TPayload}">IOutboxStore&lt;object&gt;</see>
    /// as their dependency.
    /// When <paramref name="headers"/> is <see langword="null"/>, the message is enqueued
    /// with <see cref="MessageHeaders.Empty"/> (no allocation).
    /// </remarks>
    /// <param name="store">The outbox store to enqueue into.</param>
    /// <param name="payload">The untyped payload to enqueue.</param>
    /// <param name="headers">
    /// Optional string-to-string headers. Pass <see langword="null"/> to use empty headers.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored <see cref="OutboxMessage{TPayload}">OutboxMessage&lt;object&gt;</see>.</returns>
    public static ValueTask<OutboxMessage<object>> EnqueueObjectAsync(
        this IOutboxStore<object> store,
        object payload,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct = default)
    {
        if (store is null)
            throw new ArgumentNullException(nameof(store));
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        MessageHeaders messageHeaders;
        if (headers is null || headers.Count == 0)
        {
            messageHeaders = MessageHeaders.Empty;
        }
        else
        {
            // Build from the IReadOnlyDictionary<string,string> by projecting to the
            // object? value shape that MessageHeaders accepts.
            messageHeaders = new MessageHeaders(
                headers.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)));
        }

        var message = new Message<object>(payload, messageHeaders);
        return store.EnqueueAsync(message, cancellationToken: ct);
    }
}
