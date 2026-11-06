using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class RoutingSlipTests
{
    [Scenario("Execute RunsStepsInOrderAndReturnsFinalMessage")]
    [Fact]
    public void Execute_RunsStepsInOrderAndReturnsFinalMessage()
    {
        var log = new List<string>();
        var slip = RoutingSlip<Order>.Create()
            .Step("validate", (m, ctx) =>
            {
                log.Add($"{ctx.Headers.GetString(MessageHeaderNames.RoutingSlipIndex)}:validate");
                return m.WithPayload(m.Payload with { Status = "validated" });
            })
            .Step("reserve", (m, ctx) =>
            {
                log.Add($"{ctx.Headers.GetString(MessageHeaderNames.RoutingSlipIndex)}:reserve");
                return m.WithPayload(m.Payload with { Status = $"{m.Payload.Status},reserved" });
            })
            .Build();

        var result = slip.Execute(Message<Order>.Create(new Order("order-1", "new")));

        ScenarioExpect.Equal("validated,reserved", result.Message.Payload.Status);
        ScenarioExpect.Equal(["0:validate", "1:reserve"], log);
        ScenarioExpect.Equal(["validate", "reserve"], result.CompletedSteps);
        ScenarioExpect.Equal(2, result.Count);
    }

    [Scenario("Execute WritesItineraryProgressAndCompletedHeaders")]
    [Fact]
    public void Execute_WritesItineraryProgressAndCompletedHeaders()
    {
        var slip = RoutingSlip<Order>.Create()
            .Step("validate", (m, _) => m)
            .Step("ship", (m, _) => m)
            .Build();

        var result = slip.Execute(Message<Order>.Create(new Order("order-1", "new")));

        ScenarioExpect.True(result.Message.Headers.TryGet<string[]>(MessageHeaderNames.RoutingSlip, out var itinerary));
        ScenarioExpect.True(result.Message.Headers.TryGet<string[]>(MessageHeaderNames.RoutingSlipCompleted, out var completed));
        ScenarioExpect.Equal(["validate", "ship"], itinerary!);
        ScenarioExpect.Equal(["validate", "ship"], completed!);
        ScenarioExpect.Equal(2, result.Message.Headers[MessageHeaderNames.RoutingSlipIndex]);
    }

    [Scenario("Execute ClearsPreviousCompletedHeader")]
    [Fact]
    public void Execute_ClearsPreviousCompletedHeader()
    {
        var slip = RoutingSlip<Order>.Create()
            .Step("validate", (m, _) => m)
            .Build();
        var message = Message<Order>
            .Create(new Order("order-1", "new"))
            .WithHeader(MessageHeaderNames.RoutingSlipCompleted, new[] { "old" });

        var result = slip.Execute(message);

        ScenarioExpect.Equal(["validate"], ScenarioExpect.IsType<string[]>(result.Message.Headers[MessageHeaderNames.RoutingSlipCompleted]));
    }

    [Scenario("Execute AllowsEmptyItinerary")]
    [Fact]
    public void Execute_AllowsEmptyItinerary()
    {
        var message = Message<Order>.Create(new Order("order-1", "new"));
        var result = RoutingSlip<Order>.Create().Build().Execute(message);

        ScenarioExpect.Same(message.Payload, result.Message.Payload);
        ScenarioExpect.Empty(result.CompletedSteps);
        ScenarioExpect.Equal(0, result.Message.Headers[MessageHeaderNames.RoutingSlipIndex]);
    }

    [Scenario("Execute RejectsNullMessage")]
    [Fact]
    public void Execute_RejectsNullMessage()
    {
        var slip = RoutingSlip<Order>.Create().Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => slip.Execute(null!));
    }

    [Scenario("Execute RejectsNullStepResult")]
    [Fact]
    public void Execute_RejectsNullStepResult()
    {
        var slip = RoutingSlip<Order>.Create()
            .Step("bad", (_, _) => null!)
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => slip.Execute(Message<Order>.Create(new Order("order-1", "new"))));
    }

    [Scenario("Builder RejectsInvalidArguments")]
    [Fact]
    public void Builder_RejectsInvalidArguments()
    {
        var builder = RoutingSlip<Order>.Create();

        ScenarioExpect.Throws<ArgumentException>(() => builder.Step("", (m, _) => m));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.Step("validate", null!));
    }

    [Scenario("RoutingSlipResult RejectsInvalidArgumentsAndCopiesSteps")]
    [Fact]
    public void RoutingSlipResult_RejectsInvalidArgumentsAndCopiesSteps()
    {
        var steps = new List<string> { "validate" };
        var result = new RoutingSlipResult<Order>(Message<Order>.Create(new Order("order-1", "new")), steps);

        steps.Add("ship");

        ScenarioExpect.Equal(["validate"], result.CompletedSteps);
        ScenarioExpect.Throws<ArgumentNullException>(() => new RoutingSlipResult<Order>(null!, steps));
        ScenarioExpect.Throws<ArgumentNullException>(() => new RoutingSlipResult<Order>(Message<Order>.Create(new Order("order-1", "new")), null!));
    }

    [Scenario("AsyncExecute RunsStepsInOrder")]
    [Fact]
    public async Task AsyncExecute_RunsStepsInOrder()
    {
        var log = new List<string>();
        var slip = AsyncRoutingSlip<Order>.Create()
            .Step("validate", (m, _, _) =>
            {
                log.Add("validate");
                return new ValueTask<Message<Order>>(m.WithPayload(m.Payload with { Status = "validated" }));
            })
            .Step("ship", (m, _, _) =>
            {
                log.Add("ship");
                return new ValueTask<Message<Order>>(m.WithPayload(m.Payload with { Status = $"{m.Payload.Status},shipped" }));
            })
            .Build();

        var result = await slip.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new")));

        ScenarioExpect.Equal(["validate", "ship"], log);
        ScenarioExpect.Equal("validated,shipped", result.Message.Payload.Status);
        ScenarioExpect.Equal(["validate", "ship"], result.CompletedSteps);
    }

    [Scenario("AsyncExecute ObservesCancellation")]
    [Fact]
    public async Task AsyncExecute_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var slip = AsyncRoutingSlip<Order>.Create()
            .Step("validate", (m, _, _) => new ValueTask<Message<Order>>(m))
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () =>
            await slip.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new")), cancellationToken: cts.Token));
    }

    [Scenario("AsyncExecute PreservesProvidedContextCancellationWhenNoTokenIsSupplied")]
    [Fact]
    public async Task AsyncExecute_PreservesProvidedContextCancellationWhenNoTokenIsSupplied()
    {
        using var cts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var slip = AsyncRoutingSlip<Order>.Create()
            .Step("validate", (m, ctx, token) =>
            {
                seenToken = ctx.CancellationToken;
                ScenarioExpect.Equal(CancellationToken.None, token);
                return new ValueTask<Message<Order>>(m);
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, cts.Token);

        await slip.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new")), context);

        ScenarioExpect.Equal(cts.Token, seenToken);
    }

    [Scenario("AsyncExecute UsesExplicitCancellationTokenOverProvidedContext")]
    [Fact]
    public async Task AsyncExecute_UsesExplicitCancellationTokenOverProvidedContext()
    {
        using var contextCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var slip = AsyncRoutingSlip<Order>.Create()
            .Step("validate", (m, ctx, _) =>
            {
                seenToken = ctx.CancellationToken;
                return new ValueTask<Message<Order>>(m);
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, contextCts.Token);

        await slip.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new")), context, callCts.Token);

        ScenarioExpect.Equal(callCts.Token, seenToken);
    }

    [Scenario("AsyncExecute RejectsNullMessageAndStepResult")]
    [Fact]
    public async Task AsyncExecute_RejectsNullMessageAndStepResult()
    {
        var valid = AsyncRoutingSlip<Order>.Create().Build();
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await valid.ExecuteAsync(null!));

        var invalid = AsyncRoutingSlip<Order>.Create()
            .Step("bad", (_, _, _) => new ValueTask<Message<Order>>((Message<Order>)null!))
            .Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(async () =>
            await invalid.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new"))));
    }

    [Scenario("AsyncBuilder RejectsInvalidArguments")]
    [Fact]
    public void AsyncBuilder_RejectsInvalidArguments()
    {
        var builder = AsyncRoutingSlip<Order>.Create();

        ScenarioExpect.Throws<ArgumentException>(() => builder.Step("", (m, _, _) => new ValueTask<Message<Order>>(m)));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.Step("validate", null!));
    }

    private sealed record Order(string Id, string Status);
}
