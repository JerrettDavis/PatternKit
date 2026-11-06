using PatternKit.Messaging;
using PatternKit.Messaging.Sagas;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Sagas;

public sealed class SagaTests
{
    [Scenario("Handle AppliesMatchingTypedStepsInOrder")]
    [Fact]
    public void Handle_AppliesMatchingTypedStepsInOrder()
    {
        var saga = Saga<OrderState>.Create()
            .On<OrderStarted>().Then((state, message, _) => state with { OrderId = message.Payload.OrderId, Started = true })
            .On<PaymentAccepted>().Then((state, _, _) => state with { Paid = true })
            .CompleteWhen(static state => state.Started && state.Paid)
            .Build();

        var started = saga.Handle(OrderState.Empty, Message<OrderStarted>.Create(new OrderStarted("order-1")));
        var paid = saga.Handle(started.State, Message<PaymentAccepted>.Create(new PaymentAccepted("order-1")));

        ScenarioExpect.True(started.Matched);
        ScenarioExpect.False(started.Completed);
        ScenarioExpect.Equal("order-1", started.State.OrderId);
        ScenarioExpect.True(paid.Matched);
        ScenarioExpect.True(paid.Completed);
        ScenarioExpect.True(paid.State.Paid);
    }

    [Scenario("Handle UsesGuardPredicate")]
    [Fact]
    public void Handle_UsesGuardPredicate()
    {
        var saga = Saga<OrderState>.Create()
            .When<PaymentAccepted>((state, message, _) => state.OrderId == message.Payload.OrderId)
            .Then((state, _, _) => state with { Paid = true })
            .Build();

        var result = saga.Handle(new OrderState("order-1", Started: true, Paid: false), Message<PaymentAccepted>.Create(new PaymentAccepted("order-2")));

        ScenarioExpect.False(result.Matched);
        ScenarioExpect.False(result.State.Paid);
    }

    [Scenario("Handle ReturnsUnmatchedForUnknownMessageType")]
    [Fact]
    public void Handle_ReturnsUnmatchedForUnknownMessageType()
    {
        var saga = Saga<OrderState>.Create()
            .On<OrderStarted>().Then((state, _, _) => state with { Started = true })
            .Build();

        var result = saga.Handle(OrderState.Empty, Message<PaymentAccepted>.Create(new PaymentAccepted("order-1")));

        ScenarioExpect.False(result.Matched);
        ScenarioExpect.Equal(OrderState.Empty, result.State);
    }

    [Scenario("Handle PassesMessageContext")]
    [Fact]
    public void Handle_PassesMessageContext()
    {
        var saga = Saga<OrderState>.Create()
            .On<OrderStarted>().Then((state, _, context) => state with { OrderId = context.Headers.CorrelationId })
            .Build();
        var message = Message<OrderStarted>.Create(new OrderStarted("order-1")).WithCorrelationId("corr-1");

        var result = saga.Handle(OrderState.Empty, message);

        ScenarioExpect.Equal("corr-1", result.State.OrderId);
    }

    [Scenario("Handle RejectsNullMessage")]
    [Fact]
    public void Handle_RejectsNullMessage()
    {
        var saga = Saga<OrderState>.Create().Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => saga.Handle(OrderState.Empty, (Message<OrderStarted>)null!));
    }

    [Scenario("Builder RejectsNullDelegates")]
    [Fact]
    public void Builder_RejectsNullDelegates()
    {
        var builder = Saga<OrderState>.Create();

        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When<OrderStarted>(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.CompleteWhen(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.On<OrderStarted>().Then(null!));
    }

    [Scenario("AsyncHandle AppliesMatchingTypedStepsInOrder")]
    [Fact]
    public async Task AsyncHandle_AppliesMatchingTypedStepsInOrder()
    {
        var saga = AsyncSaga<OrderState>.Create()
            .On<OrderStarted>().Then((state, message, _, _) => new ValueTask<OrderState>(state with { OrderId = message.Payload.OrderId, Started = true }))
            .On<PaymentAccepted>().Then((state, _, _, _) => new ValueTask<OrderState>(state with { Paid = true }))
            .CompleteWhen(static state => state.Started && state.Paid)
            .Build();

        var started = await saga.HandleAsync(OrderState.Empty, Message<OrderStarted>.Create(new OrderStarted("order-1")));
        var paid = await saga.HandleAsync(started.State, Message<PaymentAccepted>.Create(new PaymentAccepted("order-1")));

        ScenarioExpect.True(started.Matched);
        ScenarioExpect.False(started.Completed);
        ScenarioExpect.True(paid.Completed);
        ScenarioExpect.True(paid.State.Paid);
    }

    [Scenario("AsyncHandle UsesGuardPredicate")]
    [Fact]
    public async Task AsyncHandle_UsesGuardPredicate()
    {
        var saga = AsyncSaga<OrderState>.Create()
            .When<PaymentAccepted>((state, message, _, _) => new ValueTask<bool>(state.OrderId == message.Payload.OrderId))
            .Then((state, _, _, _) => new ValueTask<OrderState>(state with { Paid = true }))
            .Build();

        var result = await saga.HandleAsync(new OrderState("order-1", Started: true, Paid: false), Message<PaymentAccepted>.Create(new PaymentAccepted("order-2")));

        ScenarioExpect.False(result.Matched);
        ScenarioExpect.False(result.State.Paid);
    }

    [Scenario("AsyncHandle ObservesCancellation")]
    [Fact]
    public async Task AsyncHandle_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var saga = AsyncSaga<OrderState>.Create()
            .On<OrderStarted>().Then((state, _, _, _) => new ValueTask<OrderState>(state with { Started = true }))
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () =>
            await saga.HandleAsync(OrderState.Empty, Message<OrderStarted>.Create(new OrderStarted("order-1")), cancellationToken: cts.Token));
    }

    [Scenario("AsyncHandle PreservesProvidedContextCancellationWhenNoTokenIsSupplied")]
    [Fact]
    public async Task AsyncHandle_PreservesProvidedContextCancellationWhenNoTokenIsSupplied()
    {
        using var cts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var saga = AsyncSaga<OrderState>.Create()
            .On<OrderStarted>().Then((state, _, context, token) =>
            {
                seenToken = context.CancellationToken;
                ScenarioExpect.Equal(CancellationToken.None, token);
                return new ValueTask<OrderState>(state);
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, cts.Token);

        await saga.HandleAsync(OrderState.Empty, Message<OrderStarted>.Create(new OrderStarted("order-1")), context);

        ScenarioExpect.Equal(cts.Token, seenToken);
    }

    [Scenario("AsyncHandle UsesExplicitCancellationTokenOverProvidedContext")]
    [Fact]
    public async Task AsyncHandle_UsesExplicitCancellationTokenOverProvidedContext()
    {
        using var contextCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var saga = AsyncSaga<OrderState>.Create()
            .On<OrderStarted>().Then((state, _, context, _) =>
            {
                seenToken = context.CancellationToken;
                return new ValueTask<OrderState>(state);
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, contextCts.Token);

        await saga.HandleAsync(OrderState.Empty, Message<OrderStarted>.Create(new OrderStarted("order-1")), context, callCts.Token);

        ScenarioExpect.Equal(callCts.Token, seenToken);
    }

    [Scenario("AsyncHandle RejectsNullMessage")]
    [Fact]
    public async Task AsyncHandle_RejectsNullMessage()
    {
        var saga = AsyncSaga<OrderState>.Create().Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await saga.HandleAsync(OrderState.Empty, (Message<OrderStarted>)null!));
    }

    [Scenario("AsyncBuilder RejectsNullDelegates")]
    [Fact]
    public void AsyncBuilder_RejectsNullDelegates()
    {
        var builder = AsyncSaga<OrderState>.Create();

        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When<OrderStarted>(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.CompleteWhen(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.On<OrderStarted>().Then(null!));
    }

    private sealed record OrderState(string? OrderId, bool Started, bool Paid)
    {
        public static OrderState Empty { get; } = new(null, Started: false, Paid: false);
    }

    private sealed record OrderStarted(string OrderId);

    private sealed record PaymentAccepted(string OrderId);
}
