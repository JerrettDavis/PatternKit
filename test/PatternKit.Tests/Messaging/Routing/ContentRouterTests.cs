using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class ContentRouterTests
{
    [Scenario("Route UsesFirstMatchingRoute")]
    [Fact]
    public void Route_UsesFirstMatchingRoute()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((m, _) => m.Payload.Total > 100).Then((_, _) => "priority")
            .When((_, _) => true).Then((_, _) => "standard")
            .Build();

        var result = router.Route(Message<Order>.Create(new Order("o-1", 150m)));

        ScenarioExpect.Equal("priority", result);
    }

    [Scenario("Route UsesDefaultWhenNothingMatches")]
    [Fact]
    public void Route_UsesDefaultWhenNothingMatches()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((m, _) => m.Payload.Total < 0).Then((_, _) => "invalid")
            .Default((_, _) => "default")
            .Build();

        var result = router.Route(Message<Order>.Create(new Order("o-1", 10m)));

        ScenarioExpect.Equal("default", result);
    }

    [Scenario("Route ThrowsWhenNothingMatchesAndNoDefaultExists")]
    [Fact]
    public void Route_ThrowsWhenNothingMatchesAndNoDefaultExists()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((m, _) => m.Payload.Total < 0).Then((_, _) => "invalid")
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => router.Route(Message<Order>.Create(new Order("o-1", 10m))));
    }

    [Scenario("Route PassesContextToPredicateAndHandler")]
    [Fact]
    public void Route_PassesContextToPredicateAndHandler()
    {
        var router = ContentRouter<Order, string>.Create()
            .When((_, ctx) => ctx.Headers.CorrelationId == "corr-1")
            .Then((m, ctx) => $"{ctx.Headers.CorrelationId}:{m.Payload.Id}")
            .Build();

        var message = Message<Order>.Create(new Order("o-1", 10m));
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("corr-1"));

        ScenarioExpect.Equal("corr-1:o-1", router.Route(message, context));
    }

    [Scenario("Builder RejectsNullDelegates")]
    [Fact]
    public void Builder_RejectsNullDelegates()
    {
        var builder = ContentRouter<Order, string>.Create();

        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.Default(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When((_, _) => true).Then(null!));
    }

    [Scenario("Route RejectsNullMessage")]
    [Fact]
    public void Route_RejectsNullMessage()
    {
        var router = ContentRouter<Order, string>.Create()
            .Default((_, _) => "default")
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => router.Route(null!));
    }

    [Scenario("AsyncRoute UsesFirstMatchingRoute")]
    [Fact]
    public async Task AsyncRoute_UsesFirstMatchingRoute()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .When((m, _, _) => new ValueTask<bool>(m.Payload.Total > 100)).Then((_, _, _) => new ValueTask<string>("priority"))
            .When((_, _, _) => new ValueTask<bool>(true)).Then((_, _, _) => new ValueTask<string>("standard"))
            .Build();

        var result = await router.RouteAsync(Message<Order>.Create(new Order("o-1", 150m)));

        ScenarioExpect.Equal("priority", result);
    }

    [Scenario("AsyncRoute UsesDefaultWhenNothingMatches")]
    [Fact]
    public async Task AsyncRoute_UsesDefaultWhenNothingMatches()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .When((m, _, _) => new ValueTask<bool>(m.Payload.Total < 0)).Then((_, _, _) => new ValueTask<string>("invalid"))
            .Default((_, _, _) => new ValueTask<string>("default"))
            .Build();

        var result = await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m)));

        ScenarioExpect.Equal("default", result);
    }

    [Scenario("AsyncRoute ObservesCancellation")]
    [Fact]
    public async Task AsyncRoute_ObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var router = AsyncContentRouter<Order, string>.Create()
            .When((_, _, _) => new ValueTask<bool>(true)).Then((_, _, _) => new ValueTask<string>("never"))
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(async () =>
            await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m)), cancellationToken: cts.Token));
    }

    [Scenario("AsyncRoute PreservesProvidedContextCancellationWhenNoTokenIsSupplied")]
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

        ScenarioExpect.Equal("ok", result);
        ScenarioExpect.Equal(cts.Token, seenToken);
    }

    [Scenario("AsyncRoute UsesExplicitCancellationTokenOverProvidedContext")]
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

        ScenarioExpect.Equal(callCts.Token, result);
    }

    [Scenario("AsyncRoute ThrowsWhenNothingMatchesAndNoDefaultExists")]
    [Fact]
    public async Task AsyncRoute_ThrowsWhenNothingMatchesAndNoDefaultExists()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .When((m, _, _) => new ValueTask<bool>(m.Payload.Total < 0)).Then((_, _, _) => new ValueTask<string>("invalid"))
            .Build();

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(async () =>
            await router.RouteAsync(Message<Order>.Create(new Order("o-1", 10m))));
    }

    [Scenario("AsyncBuilder RejectsNullDelegates")]
    [Fact]
    public void AsyncBuilder_RejectsNullDelegates()
    {
        var builder = AsyncContentRouter<Order, string>.Create();

        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.Default(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When((_, _, _) => new ValueTask<bool>(true)).Then(null!));
    }

    [Scenario("AsyncRoute RejectsNullMessage")]
    [Fact]
    public async Task AsyncRoute_RejectsNullMessage()
    {
        var router = AsyncContentRouter<Order, string>.Create()
            .Default((_, _, _) => new ValueTask<string>("default"))
            .Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await router.RouteAsync(null!));
    }

    private sealed record Order(string Id, decimal Total);
}
