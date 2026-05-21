using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Transformation;

public sealed class MessageTranslatorTests
{
    private sealed record PartnerOrderSubmitted(string PartnerOrderId, decimal Amount, string Tenant);
    private sealed record OrderSubmitted(string OrderId, decimal Total);

    [Scenario("Message translator transforms payloads and preserves headers")]
    [Fact]
    public void MessageTranslator_Transforms_Payloads_And_Preserves_Headers()
    {
        var translator = CreateTranslator();
        var message = Message<PartnerOrderSubmitted>
            .Create(new PartnerOrderSubmitted("PO-100", 42m, "northwind"))
            .WithCorrelationId("corr-1")
            .WithHeader("tenant-id", "northwind");

        var result = translator.Translate(message);

        ScenarioExpect.True(result.Translated);
        ScenarioExpect.Equal(new OrderSubmitted("PO-100", 42m), result.Message!.Payload);
        ScenarioExpect.Equal("corr-1", result.Message.Headers.CorrelationId);
        ScenarioExpect.Equal("northwind", result.Message.Headers.GetString("tenant-id"));
        ScenarioExpect.Equal("application/vnd.patternkit.order-submitted+json", result.Message.Headers.ContentType);
    }

    [Scenario("Message translator can explicitly filter headers")]
    [Fact]
    public void MessageTranslator_Can_Explicitly_Filter_Headers()
    {
        var translator = MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>
            .Create("partner-orders")
            .TranslateWith(static (message, _) => new OrderSubmitted(message.Payload.PartnerOrderId, message.Payload.Amount))
            .KeepHeaders(MessageHeaderNames.CorrelationId)
            .Build();
        var message = Message<PartnerOrderSubmitted>
            .Create(new PartnerOrderSubmitted("PO-100", 42m, "northwind"))
            .WithCorrelationId("corr-1")
            .WithHeader("tenant-id", "northwind");

        var result = translator.Translate(message);

        ScenarioExpect.True(result.Translated);
        ScenarioExpect.Equal("corr-1", result.Message!.Headers.CorrelationId);
        ScenarioExpect.False(result.Message.Headers.ContainsKey("tenant-id"));
    }

    [Scenario("Message translator reports translation failures")]
    [Fact]
    public void MessageTranslator_Reports_Translation_Failures()
    {
        var translator = MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>
            .Create("partner-orders")
            .TranslateWith(static (_, _) => throw new InvalidOperationException("partner payload was invalid"))
            .Build();

        var result = translator.Translate(Message<PartnerOrderSubmitted>.Create(new PartnerOrderSubmitted("PO-100", 42m, "northwind")));

        ScenarioExpect.True(result.Failed);
        ScenarioExpect.IsType<InvalidOperationException>(result.Exception);
        ScenarioExpect.Equal("partner payload was invalid", result.Exception!.Message);
    }

    [Scenario("Async message translator preserves cancellation")]
    [Fact]
    public async Task AsyncMessageTranslator_Preserves_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var translator = CreateTranslator();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            translator.TranslateAsync(
                Message<PartnerOrderSubmitted>.Create(new PartnerOrderSubmitted("PO-100", 42m, "northwind")),
                cancellationToken: cts.Token).AsTask());
    }

    [Scenario("Message translator rejects invalid configuration")]
    [Fact]
    public void MessageTranslator_Rejects_Invalid_Configuration()
    {
        var translator = CreateTranslator();

        ScenarioExpect.Throws<ArgumentException>(() => MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>.Create("").TranslateWith(static (message, _) => new OrderSubmitted(message.Payload.PartnerOrderId, message.Payload.Amount)).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>.Create().TranslateWith(null!));
        ScenarioExpect.Throws<ArgumentException>(() => MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>.Create().DropHeader(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>.Create().KeepHeaders(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>.Create().ConfigureHeaders(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>.Create().Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => translator.Translate(null!));
    }

    private static MessageTranslator<PartnerOrderSubmitted, OrderSubmitted> CreateTranslator()
        => MessageTranslator<PartnerOrderSubmitted, OrderSubmitted>
            .Create("partner-orders")
            .TranslateWith(static (message, _) => new OrderSubmitted(message.Payload.PartnerOrderId, message.Payload.Amount))
            .DropHeader("raw-signature")
            .SetHeader(MessageHeaderNames.ContentType, "application/vnd.patternkit.order-submitted+json")
            .Build();
}
