namespace PatternKit.Messaging;

/// <summary>
/// Immutable message envelope that pairs a payload with metadata headers.
/// </summary>
/// <typeparam name="TPayload">The payload type carried by the message.</typeparam>
public sealed class Message<TPayload>
{
    /// <summary>
    /// Creates a message envelope.
    /// </summary>
    /// <param name="payload">The payload carried by the message.</param>
    /// <param name="headers">The message metadata. When omitted, <see cref="MessageHeaders.Empty"/> is used.</param>
    public Message(TPayload payload, MessageHeaders? headers = null)
    {
        Payload = payload;
        Headers = headers ?? MessageHeaders.Empty;
    }

    /// <summary>The payload carried by the message.</summary>
    public TPayload Payload { get; }

    /// <summary>The immutable metadata associated with the message.</summary>
    public MessageHeaders Headers { get; }

    /// <summary>
    /// Creates a message envelope with no custom headers.
    /// </summary>
    public static Message<TPayload> Create(TPayload payload) => new(payload);

    /// <summary>
    /// Returns a new message with a different payload and the same headers.
    /// </summary>
    public Message<TNewPayload> WithPayload<TNewPayload>(TNewPayload payload) => new(payload, Headers);

    /// <summary>
    /// Returns a new message with the supplied headers.
    /// </summary>
    public Message<TPayload> WithHeaders(MessageHeaders headers)
    {
        if (headers is null)
            throw new ArgumentNullException(nameof(headers));

        return new Message<TPayload>(Payload, headers);
    }

    /// <summary>
    /// Returns a new message after applying <paramref name="configure"/> to the existing headers.
    /// </summary>
    public Message<TPayload> Enrich(Func<MessageHeaders, MessageHeaders> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        return WithHeaders(configure(Headers) ?? throw new InvalidOperationException("Header enrichment returned null."));
    }

    /// <summary>
    /// Returns a new message with a header value set.
    /// </summary>
    public Message<TPayload> WithHeader(string name, object? value) => WithHeaders(Headers.With(name, value));

    /// <summary>Returns a new message with the message identifier header set.</summary>
    public Message<TPayload> WithMessageId(string messageId) => WithHeaders(Headers.WithMessageId(messageId));

    /// <summary>Returns a new message with the correlation identifier header set.</summary>
    public Message<TPayload> WithCorrelationId(string correlationId) => WithHeaders(Headers.WithCorrelationId(correlationId));

    /// <summary>Returns a new message with the causation identifier header set.</summary>
    public Message<TPayload> WithCausationId(string causationId) => WithHeaders(Headers.WithCausationId(causationId));

    /// <summary>Returns a new message with the idempotency key header set.</summary>
    public Message<TPayload> WithIdempotencyKey(string idempotencyKey) => WithHeaders(Headers.WithIdempotencyKey(idempotencyKey));

    /// <summary>Returns a new message with the content type header set.</summary>
    public Message<TPayload> WithContentType(string contentType) => WithHeaders(Headers.WithContentType(contentType));

    /// <summary>Returns a new message with the reply address header set.</summary>
    public Message<TPayload> WithReplyTo(string replyTo) => WithHeaders(Headers.WithReplyTo(replyTo));
}
