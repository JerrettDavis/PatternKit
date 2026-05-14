using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class IdempotentReceiverTests
{
    [Fact]
    public async Task HandleAsync_ProcessesFirstMessageAndSuppressesDuplicate()
    {
        var calls = 0;
        var store = new InMemoryIdempotencyStore();
        var receiver = IdempotentReceiver<Order, string>.Create(
                store,
                (message, _, _) =>
                {
                    calls++;
                    return new ValueTask<string>($"processed:{message.Payload.Id}");
                })
            .Build();
        var message = Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1");

        var first = await receiver.HandleAsync(message);
        var duplicate = await receiver.HandleAsync(message);

        Assert.True(first.Processed);
        Assert.Equal("processed:order-1", first.Result);
        Assert.Equal(IdempotentReceiverStatus.Duplicate, duplicate.Status);
        Assert.Equal("idem-1", duplicate.Key);
        Assert.Equal(1, calls);
        Assert.True(store.TryGet("idem-1", out var claim));
        Assert.Equal(IdempotencyEntryStatus.Completed, claim!.Status);
    }

    [Fact]
    public async Task HandleAsync_ReplaysCompletedResultWhenConfigured()
    {
        var store = new InMemoryIdempotencyStore();
        var receiver = IdempotentReceiver<Order, string>.Create(
                store,
                (message, _, _) => new ValueTask<string>($"processed:{message.Payload.Id}"))
            .OnDuplicate(DuplicateMessagePolicy.ReplayCompleted)
            .Build();
        var message = Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1");

        var first = await receiver.HandleAsync(message);
        var duplicate = await receiver.HandleAsync(message);

        Assert.Equal(IdempotentReceiverStatus.Processed, first.Status);
        Assert.Equal(IdempotentReceiverStatus.Replayed, duplicate.Status);
        Assert.Equal("processed:order-1", duplicate.Result);
    }

    [Fact]
    public async Task HandleAsync_SuppressesDuplicateWhenStoredResultHasWrongType()
    {
        var store = new InMemoryIdempotencyStore();
        await store.TryClaimAsync("idem-1");
        await store.MarkCompletedAsync("idem-1", 42);
        var receiver = IdempotentReceiver<Order, string>.Create(
                store,
                (_, _, _) => new ValueTask<string>("processed"))
            .OnDuplicate(DuplicateMessagePolicy.ReplayCompleted)
            .Build();

        var result = await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1"));

        Assert.Equal(IdempotentReceiverStatus.Duplicate, result.Status);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task HandleAsync_RejectsMissingKeyByDefault()
    {
        var calls = 0;
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (_, _, _) =>
                {
                    calls++;
                    return new ValueTask<string>("processed");
                })
            .Build();

        var result = await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")));

        Assert.Equal(IdempotentReceiverStatus.MissingKey, result.Status);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task HandleAsync_CanProcessMissingKeyWhenConfigured()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (_, _, _) => new ValueTask<string>("processed"))
            .OnMissingKey(MissingIdempotencyKeyPolicy.Process)
            .Build();

        var result = await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")));

        Assert.Equal(IdempotentReceiverStatus.Processed, result.Status);
        Assert.Null(result.Key);
        Assert.Equal("processed", result.Result);
    }

    [Fact]
    public async Task HandleAsync_UsesCustomKeySelector()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (message, _, _) => new ValueTask<string>(message.Payload.Id))
            .KeyBy((message, _) => message.Payload.Id)
            .Build();

        var first = await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")));
        var duplicate = await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")));

        Assert.True(first.Processed);
        Assert.Equal(IdempotentReceiverStatus.Duplicate, duplicate.Status);
        Assert.Equal("order-1", duplicate.Key);
    }

    [Fact]
    public async Task HandleAsync_PreservesSuppliedContextAndAppliesExplicitCancellation()
    {
        using var source = new CancellationTokenSource();
        var observedTenant = string.Empty;
        var observedCancellation = CancellationToken.None;
        var context = new MessageContext().WithItem("tenant", "north");
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (_, ctx, cancellationToken) =>
                {
                    observedTenant = ctx.TryGetItem<string>("tenant", out var tenant) ? tenant! : string.Empty;
                    observedCancellation = cancellationToken;
                    return new ValueTask<string>("processed");
                })
            .Build();

        var result = await receiver.HandleAsync(
            Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1"),
            context,
            source.Token);

        Assert.Equal(IdempotentReceiverStatus.Processed, result.Status);
        Assert.Equal("north", observedTenant);
        Assert.True(observedCancellation.CanBeCanceled);
    }

    [Fact]
    public async Task HandleAsync_MarksFailedAndRethrowsHandlerFailure()
    {
        var store = new InMemoryIdempotencyStore();
        var receiver = IdempotentReceiver<Order, string>.Create(
                store,
                (_, _, _) => throw new InvalidOperationException("handler failed"))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1")));

        Assert.True(store.TryGet("idem-1", out var claim));
        Assert.Equal(IdempotencyEntryStatus.Failed, claim!.Status);
        Assert.Equal("handler failed", claim.FailureReason);
    }

    [Fact]
    public async Task HandleAsync_PropagatesStoreFailure()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(
                new FailingStore(),
                (_, _, _) => new ValueTask<string>("processed"))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1")));
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (_, _, _) => new ValueTask<string>("processed"))
            .Build();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await receiver.HandleAsync(
                Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1"),
                cancellationToken: source.Token));
    }

    [Fact]
    public async Task InboxProcessor_DelegatesToIdempotentReceiver()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (message, _, _) => new ValueTask<string>(message.Payload.Id))
            .Build();
        var inbox = InboxProcessor<Order, string>.Create(receiver);

        var result = await inbox.ProcessAsync(Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1"));

        Assert.Equal("order-1", result.Result);
    }

    [Fact]
    public void Builder_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => IdempotentReceiver<Order, string>.Create(null!, (_, _, _) => default).Build());
        Assert.Throws<ArgumentNullException>(() => IdempotentReceiver<Order, string>.Create(new InMemoryIdempotencyStore(), null!).Build());
        Assert.Throws<ArgumentNullException>(() => IdempotentReceiver<Order, string>.Create(new InMemoryIdempotencyStore(), (_, _, _) => default).KeyBy(null!));
        Assert.Throws<ArgumentNullException>(() => InboxProcessor<Order, string>.Create(null!));
    }

    [Fact]
    public async Task InMemoryIdempotencyStore_ValidatesKeysAndTracksCount()
    {
        var store = new InMemoryIdempotencyStore();

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.TryClaimAsync(""));
        await store.TryClaimAsync("idem-1");
        await store.MarkFailedAsync("idem-2", "failed");

        Assert.Equal(2, store.Count);
        Assert.False(store.TryGet("missing", out var missing));
        Assert.Null(missing);
        Assert.True(store.TryGet("idem-2", out var claim));
        Assert.Equal(IdempotencyEntryStatus.Failed, claim!.Status);
        Assert.Equal("failed", claim.FailureReason);
    }

    [Fact]
    public void IdempotencyClaim_ValidatesFactoryKeys()
    {
        Assert.Throws<ArgumentException>(() => IdempotencyClaim.ClaimedKey(""));
        Assert.Throws<ArgumentException>(() => IdempotencyClaim.Existing(" ", IdempotencyEntryStatus.Completed));
    }

    [Fact]
    public async Task HandleAsync_RejectsNullMessage()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(new InMemoryIdempotencyStore(), (_, _, _) => default).Build();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await receiver.HandleAsync(null!));
    }

    private sealed record Order(string Id);

    private sealed class FailingStore : IIdempotencyStore
    {
        public ValueTask<IdempotencyClaim> TryClaimAsync(string key, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("store failed");

        public ValueTask MarkCompletedAsync(string key, object? result = null, CancellationToken cancellationToken = default)
            => default;

        public ValueTask MarkFailedAsync(string key, string? reason = null, CancellationToken cancellationToken = default)
            => default;
    }
}
