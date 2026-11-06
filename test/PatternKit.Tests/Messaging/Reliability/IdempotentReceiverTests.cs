using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class IdempotentReceiverTests
{
    [Scenario("HandleAsync ProcessesFirstMessageAndSuppressesDuplicate")]
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

        ScenarioExpect.True(first.Processed);
        ScenarioExpect.Equal("processed:order-1", first.Result);
        ScenarioExpect.Equal(IdempotentReceiverStatus.Duplicate, duplicate.Status);
        ScenarioExpect.Equal("idem-1", duplicate.Key);
        ScenarioExpect.Equal(1, calls);
        ScenarioExpect.True(store.TryGet("idem-1", out var claim));
        ScenarioExpect.Equal(IdempotencyEntryStatus.Completed, claim!.Status);
    }

    [Scenario("HandleAsync ReplaysCompletedResultWhenConfigured")]
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

        ScenarioExpect.Equal(IdempotentReceiverStatus.Processed, first.Status);
        ScenarioExpect.Equal(IdempotentReceiverStatus.Replayed, duplicate.Status);
        ScenarioExpect.Equal("processed:order-1", duplicate.Result);
    }

    [Scenario("HandleAsync SuppressesDuplicateWhenStoredResultHasWrongType")]
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

        ScenarioExpect.Equal(IdempotentReceiverStatus.Duplicate, result.Status);
        ScenarioExpect.Null(result.Result);
    }

    [Scenario("HandleAsync RejectsMissingKeyByDefault")]
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

        ScenarioExpect.Equal(IdempotentReceiverStatus.MissingKey, result.Status);
        ScenarioExpect.Equal(0, calls);
    }

    [Scenario("HandleAsync CanProcessMissingKeyWhenConfigured")]
    [Fact]
    public async Task HandleAsync_CanProcessMissingKeyWhenConfigured()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (_, _, _) => new ValueTask<string>("processed"))
            .OnMissingKey(MissingIdempotencyKeyPolicy.Process)
            .Build();

        var result = await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")));

        ScenarioExpect.Equal(IdempotentReceiverStatus.Processed, result.Status);
        ScenarioExpect.Null(result.Key);
        ScenarioExpect.Equal("processed", result.Result);
    }

    [Scenario("HandleAsync UsesCustomKeySelector")]
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

        ScenarioExpect.True(first.Processed);
        ScenarioExpect.Equal(IdempotentReceiverStatus.Duplicate, duplicate.Status);
        ScenarioExpect.Equal("order-1", duplicate.Key);
    }

    [Scenario("HandleAsync PreservesSuppliedContextAndAppliesExplicitCancellation")]
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

        ScenarioExpect.Equal(IdempotentReceiverStatus.Processed, result.Status);
        ScenarioExpect.Equal("north", observedTenant);
        ScenarioExpect.True(observedCancellation.CanBeCanceled);
    }

    [Scenario("HandleAsync MarksFailedAndRethrowsHandlerFailure")]
    [Fact]
    public async Task HandleAsync_MarksFailedAndRethrowsHandlerFailure()
    {
        var store = new InMemoryIdempotencyStore();
        var receiver = IdempotentReceiver<Order, string>.Create(
                store,
                (_, _, _) => throw new InvalidOperationException("handler failed"))
            .Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(async () =>
            await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1")));

        ScenarioExpect.True(store.TryGet("idem-1", out var claim));
        ScenarioExpect.Equal(IdempotencyEntryStatus.Failed, claim!.Status);
        ScenarioExpect.Equal("handler failed", claim.FailureReason);
    }

    [Scenario("HandleAsync PropagatesStoreFailure")]
    [Fact]
    public async Task HandleAsync_PropagatesStoreFailure()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(
                new FailingStore(),
                (_, _, _) => new ValueTask<string>("processed"))
            .Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(async () =>
            await receiver.HandleAsync(Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1")));
    }

    [Scenario("HandleAsync PropagatesCancellation")]
    [Fact]
    public async Task HandleAsync_PropagatesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (_, _, _) => new ValueTask<string>("processed"))
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () =>
            await receiver.HandleAsync(
                Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1"),
                cancellationToken: source.Token));
    }

    [Scenario("InboxProcessor DelegatesToIdempotentReceiver")]
    [Fact]
    public async Task InboxProcessor_DelegatesToIdempotentReceiver()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(
                new InMemoryIdempotencyStore(),
                (message, _, _) => new ValueTask<string>(message.Payload.Id))
            .Build();
        var inbox = InboxProcessor<Order, string>.Create(receiver);

        var result = await inbox.ProcessAsync(Message<Order>.Create(new Order("order-1")).WithIdempotencyKey("idem-1"));

        ScenarioExpect.Equal("order-1", result.Result);
    }

    [Scenario("Builder RejectsInvalidArguments")]
    [Fact]
    public void Builder_RejectsInvalidArguments()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => IdempotentReceiver<Order, string>.Create(null!, (_, _, _) => default).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => IdempotentReceiver<Order, string>.Create(new InMemoryIdempotencyStore(), null!).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => IdempotentReceiver<Order, string>.Create(new InMemoryIdempotencyStore(), (_, _, _) => default).KeyBy(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => InboxProcessor<Order, string>.Create(null!));
    }

    [Scenario("InMemoryIdempotencyStore ValidatesKeysAndTracksCount")]
    [Fact]
    public async Task InMemoryIdempotencyStore_ValidatesKeysAndTracksCount()
    {
        var store = new InMemoryIdempotencyStore();

        await ScenarioExpect.ThrowsAsync<ArgumentException>(async () => await store.TryClaimAsync(""));
        await store.TryClaimAsync("idem-1");
        await store.MarkFailedAsync("idem-2", "failed");

        ScenarioExpect.Equal(2, store.Count);
        ScenarioExpect.False(store.TryGet("missing", out var missing));
        ScenarioExpect.Null(missing);
        ScenarioExpect.True(store.TryGet("idem-2", out var claim));
        ScenarioExpect.Equal(IdempotencyEntryStatus.Failed, claim!.Status);
        ScenarioExpect.Equal("failed", claim.FailureReason);
    }

    [Scenario("IdempotencyClaim ValidatesFactoryKeys")]
    [Fact]
    public void IdempotencyClaim_ValidatesFactoryKeys()
    {
        ScenarioExpect.Throws<ArgumentException>(() => IdempotencyClaim.ClaimedKey(""));
        ScenarioExpect.Throws<ArgumentException>(() => IdempotencyClaim.Existing(" ", IdempotencyEntryStatus.Completed));
    }

    [Scenario("HandleAsync RejectsNullMessage")]
    [Fact]
    public async Task HandleAsync_RejectsNullMessage()
    {
        var receiver = IdempotentReceiver<Order, string>.Create(new InMemoryIdempotencyStore(), (_, _, _) => default).Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await receiver.HandleAsync(null!));
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
