using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Channels;

public sealed class MessageChannelTests
{
    [Scenario("SendAndReceive PreservesMessageOrder")]
    [Fact]
    public void SendAndReceive_PreservesMessageOrder()
    {
        var channel = MessageChannel<Command>.Create("inventory").Build();

        var first = channel.Send(Message<Command>.Create(new("sku-1", 3)));
        var second = channel.Send(Message<Command>.Create(new("sku-2", 5)));
        var received = channel.TryReceive();

        ScenarioExpect.True(first.Accepted);
        ScenarioExpect.Equal(2, second.Count);
        ScenarioExpect.True(received.Received);
        ScenarioExpect.Equal("sku-1", received.Message!.Payload.Sku);
        ScenarioExpect.Equal(1, received.Count);
    }

    [Scenario("BoundedChannel RejectsWhenFull")]
    [Fact]
    public void BoundedChannel_RejectsWhenFull()
    {
        var channel = MessageChannel<Command>.Create()
            .WithCapacity(1)
            .Build();

        var accepted = channel.Send(Message<Command>.Create(new("sku-1", 3)));
        var rejected = channel.Send(Message<Command>.Create(new("sku-2", 5)));

        ScenarioExpect.True(accepted.Accepted);
        ScenarioExpect.False(rejected.Accepted);
        ScenarioExpect.Equal("Channel capacity has been reached.", rejected.RejectionReason);
        ScenarioExpect.Equal("sku-1", ScenarioExpect.Single(channel.Snapshot()).Payload.Sku);
    }

    [Scenario("BoundedChannel DropsOldestWhenConfigured")]
    [Fact]
    public void BoundedChannel_DropsOldestWhenConfigured()
    {
        var channel = MessageChannel<Command>.Create()
            .WithCapacity(1, MessageChannelBackpressurePolicy.DropOldest)
            .Build();

        channel.Send(Message<Command>.Create(new("sku-1", 3)));
        var accepted = channel.Send(Message<Command>.Create(new("sku-2", 5)));

        ScenarioExpect.True(accepted.Accepted);
        ScenarioExpect.Equal("sku-2", channel.TryReceive().Message!.Payload.Sku);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageChannel<Command>.Create(""));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => MessageChannel<Command>.Create().WithCapacity(0));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageChannel<Command>.Create().Build().Send(null!));
    }

    public sealed record Command(string Sku, int Quantity);
}
