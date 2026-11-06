using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class OutboxTests
{
    [Scenario("EnqueueAsync AddsOutboxRecord")]
    [Fact]
    public async Task EnqueueAsync_AddsOutboxRecord()
    {
        var outbox = new InMemoryOutbox<Order>();
        var createdAt = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

        var record = await outbox.EnqueueAsync(
            Message<Order>.Create(new Order("order-1")),
            "outbox-1",
            createdAt);

        ScenarioExpect.Equal("outbox-1", record.Id);
        ScenarioExpect.Equal(createdAt, record.CreatedAt);
        ScenarioExpect.False(record.Dispatched);
        ScenarioExpect.Single(outbox.Records);
    }

    [Scenario("DispatchPendingAsync DispatchesPendingRecordsAndMarksThemDispatched")]
    [Fact]
    public async Task DispatchPendingAsync_DispatchesPendingRecordsAndMarksThemDispatched()
    {
        var outbox = new InMemoryOutbox<Order>();
        var dispatcher = new RecordingDispatcher();
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "outbox-1");
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-2")), "outbox-2");

        var dispatched = await outbox.DispatchPendingAsync(dispatcher);
        var secondPass = await outbox.DispatchPendingAsync(dispatcher);

        ScenarioExpect.Equal(2, dispatched);
        ScenarioExpect.Equal(0, secondPass);
        ScenarioExpect.Equal(["order-1", "order-2"], dispatcher.Dispatched);
        ScenarioExpect.All(outbox.Records, record =>
        {
            ScenarioExpect.True(record.Dispatched);
            ScenarioExpect.Equal(1, record.Attempts);
            ScenarioExpect.Null(record.LastError);
        });
    }

    [Scenario("DispatchPendingAsync RecordsFailedAttemptAndRethrows")]
    [Fact]
    public async Task DispatchPendingAsync_RecordsFailedAttemptAndRethrows()
    {
        var outbox = new InMemoryOutbox<Order>();
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "outbox-1");
        var dispatcher = new FailingDispatcher();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(async () => await outbox.DispatchPendingAsync(dispatcher));

        var record = ScenarioExpect.Single(outbox.Records);
        ScenarioExpect.False(record.Dispatched);
        ScenarioExpect.Equal(1, record.Attempts);
        ScenarioExpect.Equal("dispatch failed", record.LastError);
    }

    [Scenario("DispatchPendingAsync ObservesCancellation")]
    [Fact]
    public async Task DispatchPendingAsync_ObservesCancellation()
    {
        var outbox = new InMemoryOutbox<Order>();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "outbox-1");

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () =>
            await outbox.DispatchPendingAsync(new RecordingDispatcher(), source.Token));
    }

    [Scenario("OutboxMessage ValidatesArguments")]
    [Fact]
    public void OutboxMessage_ValidatesArguments()
    {
        ScenarioExpect.Throws<ArgumentException>(() => new OutboxMessage<Order>("", Message<Order>.Create(new Order("order-1")), DateTimeOffset.UtcNow));
        ScenarioExpect.Throws<ArgumentNullException>(() => new OutboxMessage<Order>("outbox-1", null!, DateTimeOffset.UtcNow));
    }

    [Scenario("EnqueueAsync ValidatesArguments")]
    [Fact]
    public async Task EnqueueAsync_ValidatesArguments()
    {
        var outbox = new InMemoryOutbox<Order>();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await outbox.EnqueueAsync(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await outbox.DispatchPendingAsync(null!));
    }

    private sealed record Order(string Id);

    private sealed class RecordingDispatcher : IOutboxDispatcher<Order>
    {
        internal List<string> Dispatched { get; } = new();

        public ValueTask DispatchAsync(OutboxMessage<Order> message, CancellationToken cancellationToken = default)
        {
            Dispatched.Add(message.Message.Payload.Id);
            return default;
        }
    }

    private sealed class FailingDispatcher : IOutboxDispatcher<Order>
    {
        public ValueTask DispatchAsync(OutboxMessage<Order> message, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("dispatch failed");
    }
}
