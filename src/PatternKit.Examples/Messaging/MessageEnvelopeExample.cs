using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
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
    public static Summary Run() => RunFluent();

    /// <summary>
    /// Builds the message envelope through the fluent runtime API.
    /// </summary>
    public static Summary RunFluent()
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
            "fluent",
            message.Payload.OrderId,
            message.Headers.MessageId!,
            message.Headers.CorrelationId!,
            message.Headers.CausationId!,
            message.Headers.IdempotencyKey!,
            message.Headers.ContentType!,
            context.Headers.GetString("route")!,
            attempt);
    }

    /// <summary>
    /// Builds the same message envelope through a generated contract factory.
    /// </summary>
    public static Summary RunGenerated()
    {
        var message = GeneratedOrderAcceptedEnvelope.CreateAccepted(
            new OrderAccepted("order-42", 199.95m),
            "msg-100",
            "order-42",
            "checkout-7",
            "order-42:accepted",
            "application/vnd.patternkit.order+json");

        var context = GeneratedOrderAcceptedEnvelope
            .CreateContext(message)
            .WithHeader("route", "billing")
            .WithItem("attempt", 1);

        context.TryGetItem<int>("attempt", out var attempt);

        return new Summary(
            "source-generated",
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

/// <summary>
/// Registers the message-envelope example with a standard .NET service collection.
/// </summary>
public static class MessageEnvelopeExampleServiceCollectionExtensions
{
    /// <summary>Adds the generated and fluent message-envelope example runner.</summary>
    public static IServiceCollection AddMessageEnvelopeExample(this IServiceCollection services)
        => services.AddSingleton(new MessageEnvelopeExampleRunner(MessageEnvelopeExample.RunFluent, MessageEnvelopeExample.RunGenerated));
}

/// <summary>DI-importable runner for the message-envelope example.</summary>
public sealed record MessageEnvelopeExampleRunner(Func<Summary> RunFluent, Func<Summary> RunGenerated);

[GenerateMessageEnvelope(typeof(OrderAccepted), FactoryName = "CreateAccepted")]
[MessageEnvelopeHeader("message-id", typeof(string), ParameterName = "messageId")]
[MessageEnvelopeHeader("correlation-id", typeof(string), ParameterName = "correlationId")]
[MessageEnvelopeHeader("causation-id", typeof(string), ParameterName = "causationId")]
[MessageEnvelopeHeader("idempotency-key", typeof(string), ParameterName = "idempotencyKey")]
[MessageEnvelopeHeader("content-type", typeof(string), ParameterName = "contentType")]
public static partial class GeneratedOrderAcceptedEnvelope;

/// <summary>Example payload for the message envelope demo.</summary>
public sealed record OrderAccepted(string OrderId, decimal Total);

/// <summary>Example output for the message envelope demo.</summary>
public sealed record Summary(
    string Path,
    string OrderId,
    string MessageId,
    string CorrelationId,
    string CausationId,
    string IdempotencyKey,
    string ContentType,
    string Route,
    int Attempt);
