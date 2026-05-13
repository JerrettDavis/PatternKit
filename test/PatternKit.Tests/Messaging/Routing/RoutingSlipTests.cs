using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class RoutingSlipTests
{
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

        Assert.Equal("validated,reserved", result.Message.Payload.Status);
        Assert.Equal(["0:validate", "1:reserve"], log);
        Assert.Equal(["validate", "reserve"], result.CompletedSteps);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Execute_WritesItineraryProgressAndCompletedHeaders()
    {
        var slip = RoutingSlip<Order>.Create()
            .Step("validate", (m, _) => m)
            .Step("ship", (m, _) => m)
            .Build();

        var result = slip.Execute(Message<Order>.Create(new Order("order-1", "new")));

        Assert.True(result.Message.Headers.TryGet<string[]>(MessageHeaderNames.RoutingSlip, out var itinerary));
        Assert.True(result.Message.Headers.TryGet<string[]>(MessageHeaderNames.RoutingSlipCompleted, out var completed));
        Assert.Equal(["validate", "ship"], itinerary!);
        Assert.Equal(["validate", "ship"], completed!);
        Assert.Equal(2, result.Message.Headers[MessageHeaderNames.RoutingSlipIndex]);
    }

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

        Assert.Equal(["validate"], Assert.IsType<string[]>(result.Message.Headers[MessageHeaderNames.RoutingSlipCompleted]));
    }

    [Fact]
    public void Execute_AllowsEmptyItinerary()
    {
        var message = Message<Order>.Create(new Order("order-1", "new"));
        var result = RoutingSlip<Order>.Create().Build().Execute(message);

        Assert.Same(message.Payload, result.Message.Payload);
        Assert.Empty(result.CompletedSteps);
        Assert.Equal(0, result.Message.Headers[MessageHeaderNames.RoutingSlipIndex]);
    }

    [Fact]
    public void Execute_RejectsNullMessage()
    {
        var slip = RoutingSlip<Order>.Create().Build();

        Assert.Throws<ArgumentNullException>(() => slip.Execute(null!));
    }

    [Fact]
    public void Execute_RejectsNullStepResult()
    {
        var slip = RoutingSlip<Order>.Create()
            .Step("bad", (_, _) => null!)
            .Build();

        Assert.Throws<InvalidOperationException>(() => slip.Execute(Message<Order>.Create(new Order("order-1", "new"))));
    }

    [Fact]
    public void Builder_RejectsInvalidArguments()
    {
        var builder = RoutingSlip<Order>.Create();

        Assert.Throws<ArgumentException>(() => builder.Step("", (m, _) => m));
        Assert.Throws<ArgumentNullException>(() => builder.Step("validate", null!));
    }

    [Fact]
    public void RoutingSlipResult_RejectsInvalidArgumentsAndCopiesSteps()
    {
        var steps = new List<string> { "validate" };
        var result = new RoutingSlipResult<Order>(Message<Order>.Create(new Order("order-1", "new")), steps);

        steps.Add("ship");

        Assert.Equal(["validate"], result.CompletedSteps);
        Assert.Throws<ArgumentNullException>(() => new RoutingSlipResult<Order>(null!, steps));
        Assert.Throws<ArgumentNullException>(() => new RoutingSlipResult<Order>(Message<Order>.Create(new Order("order-1", "new")), null!));
    }

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

        Assert.Equal(["validate", "ship"], log);
        Assert.Equal("validated,shipped", result.Message.Payload.Status);
        Assert.Equal(["validate", "ship"], result.CompletedSteps);
    }

    [Fact]
    public async Task AsyncExecute_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var slip = AsyncRoutingSlip<Order>.Create()
            .Step("validate", (m, _, _) => new ValueTask<Message<Order>>(m))
            .Build();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await slip.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new")), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task AsyncExecute_PreservesProvidedContextCancellationWhenNoTokenIsSupplied()
    {
        using var cts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var slip = AsyncRoutingSlip<Order>.Create()
            .Step("validate", (m, ctx, token) =>
            {
                seenToken = ctx.CancellationToken;
                Assert.Equal(CancellationToken.None, token);
                return new ValueTask<Message<Order>>(m);
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, cts.Token);

        await slip.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new")), context);

        Assert.Equal(cts.Token, seenToken);
    }

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

        Assert.Equal(callCts.Token, seenToken);
    }

    [Fact]
    public async Task AsyncExecute_RejectsNullMessageAndStepResult()
    {
        var valid = AsyncRoutingSlip<Order>.Create().Build();
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await valid.ExecuteAsync(null!));

        var invalid = AsyncRoutingSlip<Order>.Create()
            .Step("bad", (_, _, _) => new ValueTask<Message<Order>>((Message<Order>)null!))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await invalid.ExecuteAsync(Message<Order>.Create(new Order("order-1", "new"))));
    }

    [Fact]
    public void AsyncBuilder_RejectsInvalidArguments()
    {
        var builder = AsyncRoutingSlip<Order>.Create();

        Assert.Throws<ArgumentException>(() => builder.Step("", (m, _, _) => new ValueTask<Message<Order>>(m)));
        Assert.Throws<ArgumentNullException>(() => builder.Step("validate", null!));
    }

    private sealed record Order(string Id, string Status);
}
