using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class RecipientListTests
{
    [Fact]
    public void Dispatch_SendsToMatchingRecipientsInOrder()
    {
        var log = new List<string>();
        var list = RecipientList<Order>.Create()
            .When("audit", (_, _) => true).Then((_, _) => log.Add("audit"))
            .When("billing", (m, _) => m.Payload.Total > 0).Then((_, _) => log.Add("billing"))
            .When("fraud", (m, _) => m.Payload.Total > 500).Then((_, _) => log.Add("fraud"))
            .Build();

        var result = list.Dispatch(Message<Order>.Create(new Order("o-1", 100m)));

        Assert.Equal(["audit", "billing"], log);
        Assert.Equal(["audit", "billing"], result.DeliveredRecipients);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Dispatch_ReturnsEmptyResultWhenNoRecipientsMatch()
    {
        var list = RecipientList<Order>.Create()
            .When("fraud", (m, _) => m.Payload.Total > 500).Then((_, _) => { })
            .Build();

        var result = list.Dispatch(Message<Order>.Create(new Order("o-1", 100m)));

        Assert.Empty(result.DeliveredRecipients);
        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void To_AddsUnconditionalRecipient()
    {
        var delivered = false;
        var list = RecipientList<Order>.Create()
            .To("audit", (_, _) => delivered = true)
            .Build();

        var result = list.Dispatch(Message<Order>.Create(new Order("o-1", 100m)));

        Assert.True(delivered);
        Assert.Equal(["audit"], result.DeliveredRecipients);
    }

    [Fact]
    public void Dispatch_RejectsNullMessage()
    {
        var list = RecipientList<Order>.Create().To("audit", (_, _) => { }).Build();

        Assert.Throws<ArgumentNullException>(() => list.Dispatch(null!));
    }

    [Fact]
    public void Builder_RejectsInvalidArguments()
    {
        var builder = RecipientList<Order>.Create();

        Assert.Throws<ArgumentException>(() => builder.When("", (_, _) => true));
        Assert.Throws<ArgumentNullException>(() => builder.When("audit", null!));
        Assert.Throws<ArgumentNullException>(() => builder.To("audit", null!));
        Assert.Throws<ArgumentNullException>(() => builder.When("audit", (_, _) => true).Then(null!));
    }

    [Fact]
    public void RecipientListResult_CopiesDeliveredRecipients()
    {
        var delivered = new List<string> { "audit" };
        var result = new RecipientListResult(delivered);

        delivered.Add("billing");

        Assert.Equal(["audit"], result.DeliveredRecipients);
    }

    [Fact]
    public void RecipientListResult_RejectsNullRecipients()
    {
        Assert.Throws<ArgumentNullException>(() => new RecipientListResult(null!));
    }

    [Fact]
    public async Task AsyncDispatch_SendsToMatchingRecipientsInOrder()
    {
        var log = new List<string>();
        var list = AsyncRecipientList<Order>.Create()
            .When("audit", (_, _, _) => new ValueTask<bool>(true)).Then((_, _, _) =>
            {
                log.Add("audit");
                return ValueTask.CompletedTask;
            })
            .When("billing", (m, _, _) => new ValueTask<bool>(m.Payload.Total > 0)).Then((_, _, _) =>
            {
                log.Add("billing");
                return ValueTask.CompletedTask;
            })
            .Build();

        var result = await list.DispatchAsync(Message<Order>.Create(new Order("o-1", 100m)));

        Assert.Equal(["audit", "billing"], log);
        Assert.Equal(["audit", "billing"], result.DeliveredRecipients);
    }

    [Fact]
    public async Task AsyncDispatch_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var list = AsyncRecipientList<Order>.Create()
            .To("audit", (_, _, _) => ValueTask.CompletedTask)
            .Build();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await list.DispatchAsync(Message<Order>.Create(new Order("o-1", 100m)), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task AsyncDispatch_PreservesProvidedContextCancellationWhenNoTokenIsSupplied()
    {
        using var cts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var list = AsyncRecipientList<Order>.Create()
            .To("audit", (_, ctx, token) =>
            {
                seenToken = ctx.CancellationToken;
                Assert.Equal(CancellationToken.None, token);
                return ValueTask.CompletedTask;
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, cts.Token);

        await list.DispatchAsync(Message<Order>.Create(new Order("o-1", 100m)), context);

        Assert.Equal(cts.Token, seenToken);
    }

    [Fact]
    public async Task AsyncDispatch_UsesExplicitCancellationTokenOverProvidedContext()
    {
        using var contextCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var list = AsyncRecipientList<Order>.Create()
            .To("audit", (_, ctx, _) =>
            {
                seenToken = ctx.CancellationToken;
                return ValueTask.CompletedTask;
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, contextCts.Token);

        await list.DispatchAsync(Message<Order>.Create(new Order("o-1", 100m)), context, callCts.Token);

        Assert.Equal(callCts.Token, seenToken);
    }

    [Fact]
    public async Task AsyncDispatch_RejectsNullMessage()
    {
        var list = AsyncRecipientList<Order>.Create()
            .To("audit", (_, _, _) => ValueTask.CompletedTask)
            .Build();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await list.DispatchAsync(null!));
    }

    [Fact]
    public void AsyncBuilder_RejectsInvalidArguments()
    {
        var builder = AsyncRecipientList<Order>.Create();

        Assert.Throws<ArgumentException>(() => builder.When("", (_, _, _) => new ValueTask<bool>(true)));
        Assert.Throws<ArgumentNullException>(() => builder.When("audit", null!));
        Assert.Throws<ArgumentNullException>(() => builder.To("audit", null!));
        Assert.Throws<ArgumentNullException>(() => builder.When("audit", (_, _, _) => new ValueTask<bool>(true)).Then(null!));
    }

    private sealed record Order(string Id, decimal Total);
}
