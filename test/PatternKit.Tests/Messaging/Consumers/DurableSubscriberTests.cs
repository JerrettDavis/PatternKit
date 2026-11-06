using PatternKit.Messaging;
using PatternKit.Messaging.Consumers;
using PatternKit.Messaging.Storage;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Consumers;

public sealed class DurableSubscriberTests
{
    [Scenario("CatchUp ReplaysOnlyUncheckpointedMessages")]
    [Fact]
    public void CatchUp_ReplaysOnlyUncheckpointedMessages()
    {
        var store = CreateStore();
        var checkpoints = new InMemoryDurableSubscriberCheckpointStore();
        var projected = new List<string>();
        _ = store.Append(Message<Order>.Create(new("order-1")).WithMessageId("m1"));
        _ = store.Append(Message<Order>.Create(new("order-2")).WithMessageId("m2"));
        var subscriber = DurableSubscriber<Order>.Create("shipping")
            .From(store)
            .TrackWith(checkpoints)
            .Handle("project", (stored, _) => projected.Add(stored.Message.Payload.Id))
            .Build();

        var first = subscriber.CatchUp();
        var second = subscriber.CatchUp();

        ScenarioExpect.True(first.Completed);
        ScenarioExpect.Equal(2, first.DeliveredCount);
        ScenarioExpect.Equal(2L, first.LastSequence);
        ScenarioExpect.Equal(["order-1", "order-2"], projected);
        ScenarioExpect.True(second.Completed);
        ScenarioExpect.Equal(0, second.DeliveredCount);
        ScenarioExpect.Equal(2, second.SkippedCount);
        ScenarioExpect.Equal(2L, checkpoints.Load("shipping").LastSequence);
    }

    [Scenario("CatchUp StopsBeforeCheckpointingFailedMessages")]
    [Fact]
    public void CatchUp_StopsBeforeCheckpointingFailedMessages()
    {
        var store = CreateStore();
        var checkpoints = new InMemoryDurableSubscriberCheckpointStore();
        _ = store.Append(Message<Order>.Create(new("order-1")).WithMessageId("m1"));
        _ = store.Append(Message<Order>.Create(new("order-2")).WithMessageId("m2"));
        var subscriber = DurableSubscriber<Order>.Create("shipping")
            .From(store)
            .TrackWith(checkpoints)
            .Handle("reject", (stored, _) => stored.Message.Payload.Id == "order-2"
                ? DurableSubscriberHandlerResult.Failure("reject", "projection unavailable")
                : DurableSubscriberHandlerResult.Success("reject"))
            .Build();

        var result = subscriber.CatchUp();

        ScenarioExpect.False(result.Completed);
        ScenarioExpect.Equal(1, result.DeliveredCount);
        ScenarioExpect.Single(result.Failures);
        ScenarioExpect.Equal(1L, checkpoints.Load("shipping").LastSequence);
    }

    [Scenario("CatchUp ContinuePolicyDoesNotCheckpointPastFailedMessage")]
    [Fact]
    public void CatchUp_ContinuePolicyDoesNotCheckpointPastFailedMessage()
    {
        var store = CreateStore();
        var checkpoints = new InMemoryDurableSubscriberCheckpointStore();
        var handled = new List<string>();
        _ = store.Append(Message<Order>.Create(new("order-1")).WithMessageId("m1"));
        _ = store.Append(Message<Order>.Create(new("order-2")).WithMessageId("m2"));
        var subscriber = DurableSubscriber<Order>.Create("shipping")
            .From(store)
            .TrackWith(checkpoints)
            .Handle("reject", (stored, _) => stored.Message.Payload.Id == "order-1"
                ? DurableSubscriberHandlerResult.Failure("reject", "projection unavailable")
                : DurableSubscriberHandlerResult.Success("reject"))
            .Handle("audit", (stored, _) => handled.Add(stored.Message.Payload.Id))
            .OnError(DurableSubscriberErrorPolicy.Continue)
            .Build();

        var result = subscriber.CatchUp();

        ScenarioExpect.False(result.Completed);
        ScenarioExpect.Equal(0, result.DeliveredCount);
        ScenarioExpect.Equal(0L, result.LastSequence);
        ScenarioExpect.Equal(0L, checkpoints.Load("shipping").LastSequence);
        ScenarioExpect.Equal(["order-1"], handled);
        ScenarioExpect.Single(result.Failures);
    }

    [Scenario("BuilderRejectsInvalidDurableSubscriberConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidDurableSubscriberConfiguration()
    {
        var store = CreateStore();
        var checkpoints = new InMemoryDurableSubscriberCheckpointStore();

        ScenarioExpect.Throws<ArgumentException>(() => DurableSubscriber<Order>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => DurableSubscriber<Order>.Create().From(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => DurableSubscriber<Order>.Create().TrackWith(null!));
        ScenarioExpect.Throws<ArgumentException>(() => DurableSubscriber<Order>.Create().Handle("", (_, _) => DurableSubscriberHandlerResult.Success("handler")));
        ScenarioExpect.Throws<ArgumentNullException>(() => DurableSubscriber<Order>.Create().Handle("handler", (DurableSubscriber<Order>.Handler)null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => DurableSubscriber<Order>.Create().TrackWith(checkpoints).Handle("handler", (_, _) => DurableSubscriberHandlerResult.Success("handler")).Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => DurableSubscriber<Order>.Create().From(store).Handle("handler", (_, _) => DurableSubscriberHandlerResult.Success("handler")).Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => DurableSubscriber<Order>.Create().From(store).TrackWith(checkpoints).Build());
        ScenarioExpect.Throws<ArgumentException>(() => DurableSubscriberHandlerResult.Success(""));
        ScenarioExpect.Throws<ArgumentException>(() => DurableSubscriberHandlerResult.Failure("handler", ""));
        ScenarioExpect.Throws<ArgumentException>(() => checkpoints.Load(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => checkpoints.Save(null!));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => new DurableSubscriberCheckpoint("shipping", -1, null, DateTimeOffset.UtcNow));
    }

    private static MessageStore<Order> CreateStore()
        => MessageStore<Order>.Create("orders")
            .IdentifyBy(static (message, _) => message.Headers.MessageId!)
            .Build();

    private sealed record Order(string Id);
}
