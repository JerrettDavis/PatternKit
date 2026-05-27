using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Channels;

public sealed class MessageBusTests
{
    [Scenario("Publish DeliversToAllSubscribedChannels")]
    [Fact]
    public void Publish_DeliversToAllSubscribedChannels()
    {
        var fulfillment = MessageChannel<OrderEvent>.Create("fulfillment").Build();
        var audit = MessageChannel<OrderEvent>.Create("audit").Build();
        var bus = MessageBus<OrderEvent>.Create("orders")
            .Route("accepted", fulfillment)
            .Route("accepted", audit)
            .Build();

        var result = bus.Publish("accepted", Message<OrderEvent>.Create(new("O-100", "accepted")));

        ScenarioExpect.Equal(2, result.AcceptedCount);
        ScenarioExpect.Equal(0, result.RejectedCount);
        ScenarioExpect.Equal("O-100", fulfillment.TryReceive().Message!.Payload.OrderId);
        ScenarioExpect.Equal("O-100", audit.TryReceive().Message!.Payload.OrderId);
    }

    [Scenario("Publish CapturesRejectedDeliveries")]
    [Fact]
    public void Publish_CapturesRejectedDeliveries()
    {
        var bounded = MessageChannel<OrderEvent>.Create("bounded").WithCapacity(1).Build();
        var bus = MessageBus<OrderEvent>.Create().Route("accepted", bounded).Build();

        bus.Publish("accepted", Message<OrderEvent>.Create(new("O-100", "accepted")));
        var result = bus.Publish("accepted", Message<OrderEvent>.Create(new("O-101", "accepted")));

        var delivery = ScenarioExpect.Single(result.Deliveries);
        ScenarioExpect.False(delivery.Accepted);
        ScenarioExpect.Equal("Channel capacity has been reached.", delivery.RejectionReason);
        ScenarioExpect.Equal(1, result.RejectedCount);
    }

    [Scenario("Bus AllowsRuntimeSubscriptions")]
    [Fact]
    public void Bus_AllowsRuntimeSubscriptions()
    {
        var bus = MessageBus<OrderEvent>.Create("orders").Build();
        var audit = MessageChannel<OrderEvent>.Create("audit").Build();

        bus.Subscribe("accepted", audit);
        var result = bus.Publish("accepted", Message<OrderEvent>.Create(new("O-100", "accepted")));

        ScenarioExpect.Equal(["accepted"], bus.Topics);
        ScenarioExpect.Equal(1, result.AcceptedCount);
        ScenarioExpect.Equal("O-100", audit.TryReceive().Message!.Payload.OrderId);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        var channel = MessageChannel<OrderEvent>.Create("orders").Build();
        var bus = MessageBus<OrderEvent>.Create().Build();

        ScenarioExpect.Throws<ArgumentException>(() => MessageBus<OrderEvent>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => MessageBus<OrderEvent>.Create().Route("", channel));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageBus<OrderEvent>.Create().Route("accepted", null!));
        ScenarioExpect.Throws<ArgumentException>(() => bus.Subscribe("", channel));
        ScenarioExpect.Throws<ArgumentNullException>(() => bus.Subscribe("accepted", null!));
        ScenarioExpect.Throws<ArgumentException>(() => bus.Publish("", Message<OrderEvent>.Create(new("O-100", "accepted"))));
        ScenarioExpect.Throws<ArgumentNullException>(() => bus.Publish("accepted", null!));
    }

    public sealed record OrderEvent(string OrderId, string Status);
}
