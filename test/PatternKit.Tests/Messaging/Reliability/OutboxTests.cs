using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class OutboxTests
{
    [Fact]
    public async Task EnqueueAsync_AddsOutboxRecord()
    {
        var outbox = new InMemoryOutbox<Order>();
        var createdAt = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

        var record = await outbox.EnqueueAsync(
            Message<Order>.Create(new Order("order-1")),
            "outbox-1",
            createdAt);

        Assert.Equal("outbox-1", record.Id);
        Assert.Equal(createdAt, record.CreatedAt);
        Assert.False(record.Dispatched);
        Assert.Single(outbox.Records);
    }

    [Fact]
    public async Task DispatchPendingAsync_DispatchesPendingRecordsAndMarksThemDispatched()
    {
        var outbox = new InMemoryOutbox<Order>();
        var dispatcher = new RecordingDispatcher();
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "outbox-1");
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-2")), "outbox-2");

        var dispatched = await outbox.DispatchPendingAsync(dispatcher);
        var secondPass = await outbox.DispatchPendingAsync(dispatcher);

        Assert.Equal(2, dispatched);
        Assert.Equal(0, secondPass);
        Assert.Equal(["order-1", "order-2"], dispatcher.Dispatched);
        Assert.All(outbox.Records, record =>
        {
            Assert.True(record.Dispatched);
            Assert.Equal(1, record.Attempts);
            Assert.Null(record.LastError);
        });
    }

    [Fact]
    public async Task DispatchPendingAsync_RecordsFailedAttemptAndRethrows()
    {
        var outbox = new InMemoryOutbox<Order>();
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "outbox-1");
        var dispatcher = new FailingDispatcher();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await outbox.DispatchPendingAsync(dispatcher));

        var record = Assert.Single(outbox.Records);
        Assert.False(record.Dispatched);
        Assert.Equal(1, record.Attempts);
        Assert.Equal("dispatch failed", record.LastError);
    }

    [Fact]
    public async Task DispatchPendingAsync_ObservesCancellation()
    {
        var outbox = new InMemoryOutbox<Order>();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await outbox.EnqueueAsync(Message<Order>.Create(new Order("order-1")), "outbox-1");

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await outbox.DispatchPendingAsync(new RecordingDispatcher(), source.Token));
    }

    [Fact]
    public void OutboxMessage_ValidatesArguments()
    {
        Assert.Throws<ArgumentException>(() => new OutboxMessage<Order>("", Message<Order>.Create(new Order("order-1")), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentNullException>(() => new OutboxMessage<Order>("outbox-1", null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task EnqueueAsync_ValidatesArguments()
    {
        var outbox = new InMemoryOutbox<Order>();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await outbox.EnqueueAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await outbox.DispatchPendingAsync(null!));
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
