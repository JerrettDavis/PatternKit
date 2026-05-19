using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class RecipientListTests
{
    [Scenario("Dispatch SendsToMatchingRecipientsInOrder")]
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

        ScenarioExpect.Equal(["audit", "billing"], log);
        ScenarioExpect.Equal(["audit", "billing"], result.DeliveredRecipients);
        ScenarioExpect.Equal(2, result.Count);
    }

    [Scenario("Dispatch ReturnsEmptyResultWhenNoRecipientsMatch")]
    [Fact]
    public void Dispatch_ReturnsEmptyResultWhenNoRecipientsMatch()
    {
        var list = RecipientList<Order>.Create()
            .When("fraud", (m, _) => m.Payload.Total > 500).Then((_, _) => { })
            .Build();

        var result = list.Dispatch(Message<Order>.Create(new Order("o-1", 100m)));

        ScenarioExpect.Empty(result.DeliveredRecipients);
        ScenarioExpect.Equal(0, result.Count);
    }

    [Scenario("To AddsUnconditionalRecipient")]
    [Fact]
    public void To_AddsUnconditionalRecipient()
    {
        var delivered = false;
        var list = RecipientList<Order>.Create()
            .To("audit", (_, _) => delivered = true)
            .Build();

        var result = list.Dispatch(Message<Order>.Create(new Order("o-1", 100m)));

        ScenarioExpect.True(delivered);
        ScenarioExpect.Equal(["audit"], result.DeliveredRecipients);
    }

    [Scenario("Dispatch RejectsNullMessage")]
    [Fact]
    public void Dispatch_RejectsNullMessage()
    {
        var list = RecipientList<Order>.Create().To("audit", (_, _) => { }).Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => list.Dispatch(null!));
    }

    [Scenario("Builder RejectsInvalidArguments")]
    [Fact]
    public void Builder_RejectsInvalidArguments()
    {
        var builder = RecipientList<Order>.Create();

        ScenarioExpect.Throws<ArgumentException>(() => builder.When("", (_, _) => true));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When("audit", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.To("audit", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When("audit", (_, _) => true).Then(null!));
    }

    [Scenario("RecipientListResult CopiesDeliveredRecipients")]
    [Fact]
    public void RecipientListResult_CopiesDeliveredRecipients()
    {
        var delivered = new List<string> { "audit" };
        var result = new RecipientListResult(delivered);

        delivered.Add("billing");

        ScenarioExpect.Equal(["audit"], result.DeliveredRecipients);
    }

    [Scenario("RecipientListResult RejectsNullRecipients")]
    [Fact]
    public void RecipientListResult_RejectsNullRecipients()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => new RecipientListResult(null!));
    }

    [Scenario("AsyncDispatch SendsToMatchingRecipientsInOrder")]
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

        ScenarioExpect.Equal(["audit", "billing"], log);
        ScenarioExpect.Equal(["audit", "billing"], result.DeliveredRecipients);
    }

    [Scenario("AsyncDispatch ObservesCancellation")]
    [Fact]
    public async Task AsyncDispatch_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var list = AsyncRecipientList<Order>.Create()
            .To("audit", (_, _, _) => ValueTask.CompletedTask)
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () =>
            await list.DispatchAsync(Message<Order>.Create(new Order("o-1", 100m)), cancellationToken: cts.Token));
    }

    [Scenario("AsyncDispatch PreservesProvidedContextCancellationWhenNoTokenIsSupplied")]
    [Fact]
    public async Task AsyncDispatch_PreservesProvidedContextCancellationWhenNoTokenIsSupplied()
    {
        using var cts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var list = AsyncRecipientList<Order>.Create()
            .To("audit", (_, ctx, token) =>
            {
                seenToken = ctx.CancellationToken;
                ScenarioExpect.Equal(CancellationToken.None, token);
                return ValueTask.CompletedTask;
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, cts.Token);

        await list.DispatchAsync(Message<Order>.Create(new Order("o-1", 100m)), context);

        ScenarioExpect.Equal(cts.Token, seenToken);
    }

    [Scenario("AsyncDispatch UsesExplicitCancellationTokenOverProvidedContext")]
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

        ScenarioExpect.Equal(callCts.Token, seenToken);
    }

    [Scenario("AsyncDispatch RejectsNullMessage")]
    [Fact]
    public async Task AsyncDispatch_RejectsNullMessage()
    {
        var list = AsyncRecipientList<Order>.Create()
            .To("audit", (_, _, _) => ValueTask.CompletedTask)
            .Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await list.DispatchAsync(null!));
    }

    [Scenario("AsyncBuilder RejectsInvalidArguments")]
    [Fact]
    public void AsyncBuilder_RejectsInvalidArguments()
    {
        var builder = AsyncRecipientList<Order>.Create();

        ScenarioExpect.Throws<ArgumentException>(() => builder.When("", (_, _, _) => new ValueTask<bool>(true)));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When("audit", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.To("audit", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When("audit", (_, _, _) => new ValueTask<bool>(true)).Then(null!));
    }

    private sealed record Order(string Id, decimal Total);
}
