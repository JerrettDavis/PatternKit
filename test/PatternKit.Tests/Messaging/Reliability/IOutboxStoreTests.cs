using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class IOutboxStoreTests
{
    [Scenario("InMemoryOutboxStore EnqueueAsync AddsRecord")]
    [Fact]
    public async Task InMemoryOutboxStore_EnqueueAsync_AddsRecord()
    {
        var store = new InMemoryOutboxStore<string>();
        var message = Message<string>.Create("hello");

        var record = await store.EnqueueAsync(message);

        ScenarioExpect.NotNull(record);
        ScenarioExpect.False(record.Dispatched);
        ScenarioExpect.Equal(1, store.Records.Count);
    }

    [Scenario("InMemoryOutboxStore SnapshotPendingAsync ReturnsOnlyUndispatched")]
    [Fact]
    public async Task InMemoryOutboxStore_SnapshotPendingAsync_ReturnsOnlyUndispatched()
    {
        var store = new InMemoryOutboxStore<string>();
        var r1 = await store.EnqueueAsync(Message<string>.Create("msg-1"));
        var r2 = await store.EnqueueAsync(Message<string>.Create("msg-2"));
        await store.MarkDispatchedAsync(r1.Id, DateTimeOffset.UtcNow);

        var pending = await store.SnapshotPendingAsync();

        ScenarioExpect.Equal(1, pending.Count);
        ScenarioExpect.Equal(r2.Id, pending[0].Id);
    }

    [Scenario("InMemoryOutboxStore MarkDispatchedAsync SetsDispatchedFlag")]
    [Fact]
    public async Task InMemoryOutboxStore_MarkDispatchedAsync_SetsDispatchedFlag()
    {
        var store = new InMemoryOutboxStore<string>();
        var record = await store.EnqueueAsync(Message<string>.Create("msg"));
        var dispatchTime = DateTimeOffset.UtcNow;

        await store.MarkDispatchedAsync(record.Id, dispatchTime);

        var updated = store.Records.Single(r => r.Id == record.Id);
        ScenarioExpect.True(updated.Dispatched);
    }

    [Scenario("InMemoryOutboxStore MarkFailedAsync RecordsError")]
    [Fact]
    public async Task InMemoryOutboxStore_MarkFailedAsync_RecordsError()
    {
        var store = new InMemoryOutboxStore<string>();
        var record = await store.EnqueueAsync(Message<string>.Create("msg"));

        await store.MarkFailedAsync(record.Id, "network error");

        var updated = store.Records.Single(r => r.Id == record.Id);
        ScenarioExpect.Equal("network error", updated.LastError);
        ScenarioExpect.Equal(1, updated.Attempts);
    }

    [Scenario("OutboxDispatcher DrainAsync DispatchesAndMarksRecords")]
    [Fact]
    public async Task OutboxDispatcher_DrainAsync_DispatchesAndMarksRecords()
    {
        var store = new InMemoryOutboxStore<string>();
        await store.EnqueueAsync(Message<string>.Create("msg-1"));
        await store.EnqueueAsync(Message<string>.Create("msg-2"));

        var dispatched = new List<string>();
        var mockDispatcher = new LambdaDispatcher<string>(async (record, _) =>
        {
            dispatched.Add(record.Message.Payload);
            await Task.CompletedTask;
        });

        var dispatcher = new OutboxDispatcher<string>(store, mockDispatcher);
        var count = await dispatcher.DrainAsync();

        ScenarioExpect.Equal(2, count);
        ScenarioExpect.Equal(["msg-1", "msg-2"], dispatched);
        ScenarioExpect.Empty(await store.SnapshotPendingAsync());
    }

    [Scenario("OutboxDispatcher DrainAsync PropagatesDispatcherException")]
    [Fact]
    public async Task OutboxDispatcher_DrainAsync_PropagatesDispatcherException()
    {
        var store = new InMemoryOutboxStore<string>();
        await store.EnqueueAsync(Message<string>.Create("msg-1"));

        var mockDispatcher = new LambdaDispatcher<string>(async (_, _) =>
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("dispatch failed");
        });

        var dispatcher = new OutboxDispatcher<string>(store, mockDispatcher);

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() => dispatcher.DrainAsync().AsTask());

        var record = store.Records.Single();
        ScenarioExpect.Equal("dispatch failed", record.LastError);
    }

    [Scenario("OutboxDispatcher RunAsync DrainsContinuously")]
    [Fact]
    public async Task OutboxDispatcher_RunAsync_DrainsContinuously()
    {
        var store = new InMemoryOutboxStore<string>();
        await store.EnqueueAsync(Message<string>.Create("msg-1"));
        await store.EnqueueAsync(Message<string>.Create("msg-2"));

        using var cts = new CancellationTokenSource();
        var drainCount = 0;

        var mockDispatcher = new LambdaDispatcher<string>(async (_, _) =>
        {
            await Task.CompletedTask;
            if (Interlocked.Increment(ref drainCount) >= 2)
                cts.Cancel();
        });

        var dispatcher = new OutboxDispatcher<string>(store, mockDispatcher);
        await dispatcher.RunAsync(TimeSpan.FromMilliseconds(10), cts.Token);

        ScenarioExpect.Equal(2, drainCount);
    }

    [Scenario("OutboxDispatcher Constructor RejectsNullArguments")]
    [Fact]
    public void OutboxDispatcher_Constructor_RejectsNullArguments()
    {
        var store = new InMemoryOutboxStore<string>();
        ScenarioExpect.Throws<ArgumentNullException>(() => new OutboxDispatcher<string>(null!, new LambdaDispatcher<string>(async (_, _) => await Task.CompletedTask)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new OutboxDispatcher<string>(store, null!));
    }

    [Scenario("InMemoryOutboxStore EnqueueAsync RejectsNullMessage")]
    [Fact]
    public async Task InMemoryOutboxStore_EnqueueAsync_RejectsNullMessage()
    {
        var store = new InMemoryOutboxStore<string>();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => store.EnqueueAsync(null!).AsTask());
    }

    private sealed class LambdaDispatcher<T> : IOutboxDispatcher<T>
    {
        private readonly Func<OutboxMessage<T>, CancellationToken, ValueTask> _dispatch;

        internal LambdaDispatcher(Func<OutboxMessage<T>, CancellationToken, ValueTask> dispatch)
            => _dispatch = dispatch;

        public ValueTask DispatchAsync(OutboxMessage<T> message, CancellationToken cancellationToken = default)
            => _dispatch(message, cancellationToken);
    }
}
