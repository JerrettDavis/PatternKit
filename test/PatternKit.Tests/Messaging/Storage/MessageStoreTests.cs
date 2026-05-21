using PatternKit.Messaging;
using PatternKit.Messaging.Storage;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Storage;

public sealed class MessageStoreTests
{
    [Scenario("Append StoresMessageForLookupAndReplay")]
    [Fact]
    public void Append_StoresMessageForLookupAndReplay()
    {
        var now = new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero);
        var store = MessageStore<Order>.Create("order-audit")
            .IdentifyBy(static (message, _) => message.Headers.MessageId!)
            .UseClock(() => now)
            .Build();
        var message = Message<Order>.Create(new("order-1", 100m)).WithMessageId("msg-1").WithCorrelationId("checkout-1");

        var result = store.Append(message);
        var stored = store.Get("msg-1");
        var replay = store.Replay(MessageStoreQuery.ForCorrelation("checkout-1"));

        ScenarioExpect.True(result.Stored);
        ScenarioExpect.False(result.Duplicate);
        ScenarioExpect.Equal("order-audit", result.StoreName);
        ScenarioExpect.Equal("msg-1", result.StoredMessage.MessageId);
        ScenarioExpect.Equal(now, result.StoredMessage.StoredAtUtc);
        ScenarioExpect.NotNull(stored);
        ScenarioExpect.Equal(1L, stored!.Sequence);
        ScenarioExpect.Single(replay);
        ScenarioExpect.Equal("order-1", replay[0].Payload.Id);
    }

    [Scenario("Append DetectsDuplicateMessageIds")]
    [Fact]
    public void Append_DetectsDuplicateMessageIds()
    {
        var store = MessageStore<Order>.Create()
            .IdentifyBy(static (message, _) => message.Headers.MessageId!)
            .Build();
        var first = Message<Order>.Create(new("order-1", 100m)).WithMessageId("msg-1");
        var second = Message<Order>.Create(new("order-2", 200m)).WithMessageId("msg-1");

        _ = store.Append(first);
        var duplicate = store.Append(second);

        ScenarioExpect.False(duplicate.Stored);
        ScenarioExpect.True(duplicate.Duplicate);
        ScenarioExpect.Equal("order-1", duplicate.StoredMessage.Message.Payload.Id);
        ScenarioExpect.Single(store.Query());
    }

    [Scenario("RetentionPredicateRejectsMessagesWithoutPersisting")]
    [Fact]
    public void RetentionPredicate_RejectsMessagesWithoutPersisting()
    {
        var store = MessageStore<Order>.Create()
            .IdentifyBy(static (message, _) => message.Headers.MessageId!)
            .RetainWhen(static stored => stored.Message.Payload.Total <= 100m)
            .Build();

        var result = store.Append(Message<Order>.Create(new("order-1", 250m)).WithMessageId("msg-1"));

        ScenarioExpect.False(result.Stored);
        ScenarioExpect.False(result.Duplicate);
        ScenarioExpect.Equal("Message did not satisfy retention policy.", result.RejectionReason);
        ScenarioExpect.Empty(store.Query());
    }

    [Scenario("QuerySupportsCorrelationTimeWindowAndMaxCount")]
    [Fact]
    public void Query_SupportsCorrelationTimeWindowAndMaxCount()
    {
        var ticks = new Queue<DateTimeOffset>([
            new(2026, 5, 21, 10, 0, 0, TimeSpan.Zero),
            new(2026, 5, 21, 10, 1, 0, TimeSpan.Zero),
            new(2026, 5, 21, 10, 2, 0, TimeSpan.Zero)
        ]);
        var store = MessageStore<Order>.Create()
            .IdentifyBy(static (message, _) => message.Headers.MessageId!)
            .UseClock(() => ticks.Dequeue())
            .Build();
        _ = store.Append(Message<Order>.Create(new("order-1", 10m)).WithMessageId("m1").WithCorrelationId("c1"));
        _ = store.Append(Message<Order>.Create(new("order-2", 20m)).WithMessageId("m2").WithCorrelationId("c1"));
        _ = store.Append(Message<Order>.Create(new("order-3", 30m)).WithMessageId("m3").WithCorrelationId("c2"));

        var matches = store.Query(new MessageStoreQuery(
            "c1",
            new DateTimeOffset(2026, 5, 21, 10, 0, 30, TimeSpan.Zero),
            null,
            1));

        ScenarioExpect.Single(matches);
        ScenarioExpect.Equal("m2", matches[0].MessageId);
    }

    [Scenario("BuilderRejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageStore<Order>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageStore<Order>.Create().IdentifyBy(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageStore<Order>.Create().RetainWhen(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageStore<Order>.Create().UseClock(null!));
        ScenarioExpect.Throws<ArgumentException>(() => MessageStoreQuery.ForCorrelation(""));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => MessageStoreQuery.All.Take(0));
    }

    [Scenario("AppendRejectsNullMessageAndBlankIdentity")]
    [Fact]
    public void Append_RejectsNullMessageAndBlankIdentity()
    {
        var store = MessageStore<Order>.Create()
            .IdentifyBy(static (_, _) => "")
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => store.Append(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => store.Append(Message<Order>.Create(new("order-1", 10m))));
        ScenarioExpect.Throws<ArgumentException>(() => store.Get(""));
    }

    private sealed record Order(string Id, decimal Total);
}
