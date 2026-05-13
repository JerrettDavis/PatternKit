using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class ContentRouterTests
{
    [Fact]
    public void Route_UsesFirstMatchingRoute()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((m, _) => m.Payload.Total > 100).Then((_, _) => "priority")
            .When((_, _) => true).Then((_, _) => "standard")
            .Build();

        var result = router.Route(Message<Order>.Create(new Order("o-1", 150m)));

        Assert.Equal("priority", result);
    }

    [Fact]
    public void Route_UsesDefaultWhenNothingMatches()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((m, _) => m.Payload.Total < 0).Then((_, _) => "invalid")
            .Default((_, _) => "default")
            .Build();

        var result = router.Route(Message<Order>.Create(new Order("o-1", 10m)));

        Assert.Equal("default", result);
    }

    [Fact]
    public void Route_ThrowsWhenNothingMatchesAndNoDefaultExists()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((m, _) => m.Payload.Total < 0).Then((_, _) => "invalid")
            .Build();

        Assert.Throws<InvalidOperationException>(() => router.Route(Message<Order>.Create(new Order("o-1", 10m))));
    }

    [Fact]
    public void Route_PassesContextToPredicateAndHandler()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((_, ctx) => ctx.Headers.CorrelationId == "corr-1")
            .Then((m, ctx) => $"{ctx.Headers.CorrelationId}:{m.Payload.Id}")
            .Build();

        var message = Message<Order>.Create(new Order("o-1", 10m));
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("corr-1"));

        Assert.Equal("corr-1:o-1", router.Route(message, context));
    }

    [Fact]
    public void Builder_RejectsNullDelegates()
    {
        var builder = ContentRouter<Order, string>.Create();

        Assert.Throws<ArgumentNullException>(() => builder.When(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Default(null!));
        Assert.Throws<ArgumentNullException>(() => builder.When((_, _) => true).Then(null!));
    }

    [Fact]
    public void Route_RejectsNullMessage()
    {
        var router = ContentRouter<Order, string>.Create()
            .Default((_, _) => "default")
            .Build();

        Assert.Throws<ArgumentNullException>(() => router.Route(null!));
    }

    [Fact]
    public async Task AsyncRoute_UsesFirstMatchingRoute()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .When((m, _, _) => new ValueTask<bool>(m.Payload.Total > 100)).Then((_, _, _) => new ValueTask<string>("priority"))
            .When((_, _, _) => new ValueTask<bool>(true)).Then((_, _, _) => new ValueTask<string>("standard"))
            .Build();

        var result = await router.RouteAsync(Message<Order>.Create(new Order("o-1", 150m)));

        Assert.Equal("priority", result);
    }

    [Fact]
    public async Task AsyncRoute_UsesDefaultWhenNothingMatches()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .When((m, _, _) => new ValueTask<bool>(m.Payload.Total < 0)).Then((_, _, _) => new ValueTask<string>("invalid"))
            .Default((_, _, _) => new ValueTask<string>("default"))
            .Build();

        var result = await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m)));

        Assert.Equal("default", result);
    }

    [Fact]
    public async Task AsyncRoute_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var router = AsyncContentRouter<Order, string>.Create()
            .When((_, _, _) => new ValueTask<bool>(true)).Then((_, _, _) => new ValueTask<string>("never"))
            .Build();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m)), cancellationToken: cts.Token));
    }

    [Fact]
    public async Task AsyncRoute_PreservesProvidedContextCancellationWhenNoTokenIsSupplied()
    {
        using var cts = new CancellationTokenSource();
        var seenToken = CancellationToken.None;
        var router = AsyncContentRouter<Order, string>.Create()
            .Default((_, ctx, token) =>
            {
                seenToken = ctx.CancellationToken;
                return new ValueTask<string>(token == CancellationToken.None ? "ok" : "unexpected");
            })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, cts.Token);

        var result = await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m)), context);

        Assert.Equal("ok", result);
        Assert.Equal(cts.Token, seenToken);
    }

    [Fact]
    public async Task AsyncRoute_UsesExplicitCancellationTokenOverProvidedContext()
    {
        using var contextCts = new CancellationTokenSource();
        using var callCts = new CancellationTokenSource();
        var router = AsyncContentRouter<Order, CancellationToken>.Create()
            .Default((_, ctx, _) => new ValueTask<CancellationToken>(ctx.CancellationToken))
            .Build();
        var context = new MessageContext(MessageHeaders.Empty, contextCts.Token);

        var result = await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m)), context, callCts.Token);

        Assert.Equal(callCts.Token, result);
    }

    [Fact]
    public async Task AsyncRoute_ThrowsWhenNothingMatchesAndNoDefaultExists()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .When((m, _, _) => new ValueTask<bool>(m.Payload.Total < 0)).Then((_, _, _) => new ValueTask<string>("invalid"))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m))));
    }

    [Fact]
    public void AsyncBuilder_RejectsNullDelegates()
    {
        var builder = AsyncContentRouter<Order, string>.Create();

        Assert.Throws<ArgumentNullException>(() => builder.When(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Default(null!));
        Assert.Throws<ArgumentNullException>(() => builder.When((_, _, _) => new ValueTask<bool>(true)).Then(null!));
    }

    [Fact]
    public async Task AsyncRoute_RejectsNullMessage()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .Default((_, _, _) => new ValueTask<string>("default"))
            .Build();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await router.RouteAsync(null!));
    }

    private sealed record Order(string Id, decimal Total);
}
