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

    [Scenario("Drain EvaluatesPredicateOutsideChannelLock")]
    [Fact]
    public void Drain_EvaluatesPredicateOutsideChannelLock()
    {
        var channel = MessageChannel<Command>.Create("inventory").Build();
        channel.Send(Message<Command>.Create(new("sku-1", 3)));
        channel.Send(Message<Command>.Create(new("sku-2", 5)));

        var drained = channel.Drain(message =>
        {
            var snapshot = channel.Snapshot();
            return message.Payload.Sku == "sku-1" && snapshot.Count == 2;
        });

        ScenarioExpect.Equal("sku-1", ScenarioExpect.Single(drained).Payload.Sku);
        ScenarioExpect.Equal("sku-2", ScenarioExpect.Single(channel.Snapshot()).Payload.Sku);
    }

    [Scenario("ChannelPurger ReportsRemainingCountFromDrain")]
    [Fact]
    public void ChannelPurger_ReportsRemainingCountFromDrain()
    {
        var channel = MessageChannel<Command>.Create("inventory").Build();
        channel.Send(Message<Command>.Create(new("sku-1", 3)));
        channel.Send(Message<Command>.Create(new("sku-2", 5)));
        var purger = ChannelPurger<Command>.Create("expired")
            .From(channel)
            .When(static message => message.Payload.Sku == "sku-1")
            .Build();

        var result = purger.Purge();

        ScenarioExpect.Equal(1, result.PurgedCount);
        ScenarioExpect.Equal(1, result.RemainingCount);
        ScenarioExpect.Equal("sku-1", ScenarioExpect.Single(result.PurgedMessages).Payload.Sku);
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
