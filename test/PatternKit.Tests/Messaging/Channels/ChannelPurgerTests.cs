using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Channels;

public sealed class ChannelPurgerTests
{
    [Scenario("Purge RemovesAllMessagesAndReportsAuditRecords")]
    [Fact]
    public void Purge_RemovesAllMessagesAndReportsAuditRecords()
    {
        var audited = new List<string>();
        var channel = MessageChannel<Command>.Create("inventory-maintenance").Build();
        channel.Send(Message<Command>.Create(new("sku-1", false)));
        channel.Send(Message<Command>.Create(new("sku-2", true)));
        var purger = ChannelPurger<Command>.Create("nightly-purger")
            .From(channel)
            .AuditWith(record => audited.Add(record.Message.Payload.Sku))
            .Build();

        var result = purger.Purge();

        ScenarioExpect.Equal("nightly-purger", result.PurgerName);
        ScenarioExpect.Equal("inventory-maintenance", result.ChannelName);
        ScenarioExpect.Equal(2, result.PurgedCount);
        ScenarioExpect.Equal(0, result.RemainingCount);
        ScenarioExpect.Equal("sku-1", result.PurgedMessages[0].Payload.Sku);
        ScenarioExpect.Equal("sku-2", result.PurgedMessages[1].Payload.Sku);
        ScenarioExpect.Equal(["sku-1", "sku-2"], audited);
        ScenarioExpect.Empty(channel.Snapshot());
    }

    [Scenario("Purge RemovesOnlyMatchingMessagesAndPreservesOrder")]
    [Fact]
    public void Purge_RemovesOnlyMatchingMessagesAndPreservesOrder()
    {
        var channel = MessageChannel<Command>.Create("inventory-maintenance").Build();
        channel.Send(Message<Command>.Create(new("sku-1", false)));
        channel.Send(Message<Command>.Create(new("sku-2", true)));
        channel.Send(Message<Command>.Create(new("sku-3", false)));
        var purger = ChannelPurger<Command>.Create()
            .From(channel)
            .When(static message => message.Payload.Expired)
            .Build();

        var result = purger.Purge();
        var remaining = channel.Snapshot();

        ScenarioExpect.Equal(1, result.PurgedCount);
        ScenarioExpect.Equal(2, result.RemainingCount);
        ScenarioExpect.Equal("sku-2", ScenarioExpect.Single(result.PurgedMessages).Payload.Sku);
        ScenarioExpect.Equal("sku-1", remaining[0].Payload.Sku);
        ScenarioExpect.Equal("sku-3", remaining[1].Payload.Sku);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => ChannelPurger<Command>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChannelPurger<Command>.Create().From(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChannelPurger<Command>.Create().When(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChannelPurger<Command>.Create().AuditWith(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => ChannelPurger<Command>.Create().Build());
    }

    [Scenario("Purge PreservesChannelWhenPredicateThrows")]
    [Fact]
    public void Purge_PreservesChannelWhenPredicateThrows()
    {
        var channel = MessageChannel<Command>.Create("inventory-maintenance").Build();
        channel.Send(Message<Command>.Create(new("sku-1", false)));
        channel.Send(Message<Command>.Create(new("sku-2", true)));
        var purger = ChannelPurger<Command>.Create()
            .From(channel)
            .When(static _ => throw new InvalidOperationException("predicate failed"))
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => purger.Purge());
        var remaining = channel.Snapshot();
        ScenarioExpect.Equal(2, remaining.Count);
        ScenarioExpect.Equal("sku-1", remaining[0].Payload.Sku);
        ScenarioExpect.Equal("sku-2", remaining[1].Payload.Sku);
    }

    public sealed record Command(string Sku, bool Expired);
}
