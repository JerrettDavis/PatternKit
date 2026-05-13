namespace PatternKit.Messaging;

/// <summary>
/// Well-known header names used by PatternKit messaging and enterprise integration patterns.
/// </summary>
public static class MessageHeaderNames
{
    /// <summary>Unique identifier for the message envelope.</summary>
    public const string MessageId = "message-id";

    /// <summary>Identifier shared by messages that belong to the same logical flow.</summary>
    public const string CorrelationId = "correlation-id";

    /// <summary>Identifier of the message or operation that caused this message.</summary>
    public const string CausationId = "causation-id";

    /// <summary>Stable key used by idempotent receivers to detect duplicates.</summary>
    public const string IdempotencyKey = "idempotency-key";

    /// <summary>Payload content type, such as <c>application/json</c>.</summary>
    public const string ContentType = "content-type";

    /// <summary>Logical reply address for request/reply workflows.</summary>
    public const string ReplyTo = "reply-to";

    /// <summary>Timestamp recorded when the message was created or accepted.</summary>
    public const string Timestamp = "timestamp";
}
