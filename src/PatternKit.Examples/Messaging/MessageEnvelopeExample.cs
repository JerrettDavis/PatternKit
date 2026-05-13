using PatternKit.Messaging;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates immutable message enrichment and execution context metadata.
/// </summary>
public static class MessageEnvelopeExample
{
    /// <summary>
    /// Builds an enriched message and context, returning the metadata needed by tests and docs.
    /// </summary>
    public static Summary Run()
    {
        var message = Message<OrderAccepted>
            .Create(new OrderAccepted("order-42", 199.95m))
            .WithMessageId("msg-100")
            .WithCorrelationId("order-42")
            .WithCausationId("checkout-7")
            .WithIdempotencyKey("order-42:accepted")
            .WithContentType("application/vnd.patternkit.order+json");

        var context = MessageContext
            .From(message)
            .WithHeader("route", "billing")
            .WithItem("attempt", 1);

        context.TryGetItem<int>("attempt", out var attempt);

        return new Summary(
            message.Payload.OrderId,
            message.Headers.MessageId!,
            message.Headers.CorrelationId!,
            message.Headers.CausationId!,
            message.Headers.IdempotencyKey!,
            message.Headers.ContentType!,
            context.Headers.GetString("route")!,
            attempt);
    }
}

/// <summary>Example payload for the message envelope demo.</summary>
public sealed record OrderAccepted(string OrderId, decimal Total);

/// <summary>Example output for the message envelope demo.</summary>
public sealed record Summary(
    string OrderId,
    string MessageId,
    string CorrelationId,
    string CausationId,
    string IdempotencyKey,
    string ContentType,
    string Route,
    int Attempt);
