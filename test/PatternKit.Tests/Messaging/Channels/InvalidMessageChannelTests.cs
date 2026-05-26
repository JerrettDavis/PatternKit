using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Channels;

public sealed class InvalidMessageChannelTests
{
    [Scenario("Route SendsInvalidMessageToInvalidChannel")]
    [Fact]
    public void Route_SendsInvalidMessageToInvalidChannel()
    {
        var invalids = MessageChannel<InvalidMessage<OrderImport>>.Create("invalid-orders").Build();
        var routedAt = new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);
        var channel = InvalidMessageChannel<OrderImport>.Create("order-import-invalids")
            .To(invalids)
            .When(static message => string.IsNullOrWhiteSpace(message.Payload.Sku))
            .Because(static _ => "SKU is required.")
            .WithClock(() => routedAt)
            .Build();

        var result = channel.Route(Message<OrderImport>.Create(new("", 3)).WithCorrelationId("order-1"));

        ScenarioExpect.True(result.Routed);
        ScenarioExpect.Equal("SKU is required.", result.Reason);
        ScenarioExpect.Equal(1, result.InvalidMessageCount);
        var invalid = ScenarioExpect.Single(invalids.Snapshot()).Payload;
        ScenarioExpect.Equal("", invalid.OriginalMessage.Payload.Sku);
        ScenarioExpect.Equal("order-1", invalid.OriginalMessage.Headers.CorrelationId);
        ScenarioExpect.Equal(routedAt, invalid.RoutedAt);
    }

    [Scenario("Route LeavesValidMessageUnrouted")]
    [Fact]
    public void Route_LeavesValidMessageUnrouted()
    {
        var invalids = MessageChannel<InvalidMessage<OrderImport>>.Create("invalid-orders").Build();
        var channel = InvalidMessageChannel<OrderImport>.Create()
            .To(invalids)
            .When(static message => message.Payload.Quantity <= 0)
            .Because(static _ => "Quantity must be positive.")
            .Build();

        var result = channel.Route(Message<OrderImport>.Create(new("SKU-100", 2)));

        ScenarioExpect.False(result.Routed);
        ScenarioExpect.Null(result.InvalidMessage);
        ScenarioExpect.Equal(0, invalids.Count);
    }

    [Scenario("Builder RejectsInvalidMessageChannelConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidMessageChannelConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => InvalidMessageChannel<OrderImport>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => InvalidMessageChannel<OrderImport>.Create().To(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => InvalidMessageChannel<OrderImport>.Create().When(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => InvalidMessageChannel<OrderImport>.Create().Because(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => InvalidMessageChannel<OrderImport>.Create().WithClock(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => InvalidMessageChannel<OrderImport>.Create().Build());
    }

    [Scenario("Route RejectsBlankInvalidMessageReason")]
    [Fact]
    public void Route_RejectsBlankInvalidMessageReason()
    {
        var invalids = MessageChannel<InvalidMessage<OrderImport>>.Create("invalid-orders").Build();
        var channel = InvalidMessageChannel<OrderImport>.Create()
            .To(invalids)
            .Because(static _ => "")
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => channel.Route(Message<OrderImport>.Create(new("", 0))));
        ScenarioExpect.Equal(0, invalids.Count);
    }

    public sealed record OrderImport(string Sku, int Quantity);
}
