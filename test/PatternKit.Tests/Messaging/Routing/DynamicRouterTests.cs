using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class DynamicRouterTests
{
    [Scenario("Route UsesCurrentFirstMatchingRoute")]
    [Fact]
    public void Route_UsesCurrentFirstMatchingRoute()
    {
        var router = DynamicRouter<Order, string>.Create()
            .When("priority", 10, static (message, _) => message.Payload.Total > 100).Then(static (_, _) => "priority")
            .When("standard", 20, static (_, _) => true).Then(static (_, _) => "standard")
            .Build();

        var result = router.Route(Message<Order>.Create(new Order("o-1", 150m)));

        ScenarioExpect.Equal("priority", result);
    }

    [Scenario("Register ReplacesExistingRouteByName")]
    [Fact]
    public void Register_ReplacesExistingRouteByName()
    {
        var router = DynamicRouter<Order, string>.Create()
            .When("fulfillment", 10, static (_, _) => true).Then(static (_, _) => "old")
            .Default(static (_, _) => "default")
            .Build();

        router.Register("fulfillment", 10, static (_, _) => true, static (_, _) => "new");

        ScenarioExpect.Equal("new", router.Route(Message<Order>.Create(new Order("o-1", 10m))));
        ScenarioExpect.Equal(["fulfillment"], router.RouteNames);
    }

    [Scenario("Unregister RemovesRouteAndFallsBackToDefault")]
    [Fact]
    public void Unregister_RemovesRouteAndFallsBackToDefault()
    {
        var router = DynamicRouter<Order, string>.Create()
            .When("priority", 10, static (message, _) => message.Payload.Total > 100).Then(static (_, _) => "priority")
            .Default(static (_, _) => "default")
            .Build();

        var removed = router.Unregister("priority");
        var missing = router.Unregister("missing");

        ScenarioExpect.True(removed);
        ScenarioExpect.False(missing);
        ScenarioExpect.Equal("default", router.Route(Message<Order>.Create(new Order("o-1", 150m))));
    }

    [Scenario("Route ThrowsWhenNothingMatchesAndNoDefaultExists")]
    [Fact]
    public void Route_ThrowsWhenNothingMatchesAndNoDefaultExists()
    {
        var router = DynamicRouter<Order, string>.Create()
            .When("invalid", 10, static (message, _) => message.Payload.Total < 0).Then(static (_, _) => "invalid")
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => router.Route(Message<Order>.Create(new Order("o-1", 10m))));
    }

    [Scenario("Route PassesContextToPredicateAndHandler")]
    [Fact]
    public void Route_PassesContextToPredicateAndHandler()
    {
        var router = DynamicRouter<Order, string>.Create()
            .When("correlated", 10, static (_, context) => context.Headers.CorrelationId == "corr-1")
            .Then(static (message, context) => $"{context.Headers.CorrelationId}:{message.Payload.Id}")
            .Build();

        var message = Message<Order>.Create(new Order("o-1", 10m));
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("corr-1"));

        ScenarioExpect.Equal("corr-1:o-1", router.Route(message, context));
    }

    [Scenario("Builder RejectsInvalidDynamicRoutes")]
    [Fact]
    public void Builder_RejectsInvalidDynamicRoutes()
    {
        var builder = DynamicRouter<Order, string>.Create();

        ScenarioExpect.Throws<ArgumentException>(() => builder.When("", 10, static (_, _) => true));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When("priority", 10, null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.Default(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.When("priority", 10, static (_, _) => true).Then(null!));
    }

    [Scenario("Router RejectsInvalidRuntimeOperations")]
    [Fact]
    public void Router_RejectsInvalidRuntimeOperations()
    {
        var router = DynamicRouter<Order, string>.Create()
            .Default(static (_, _) => "default")
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => router.Route(null!));
        ScenarioExpect.Throws<ArgumentException>(() => router.Register("", 10, static (_, _) => true, static (_, _) => "bad"));
        ScenarioExpect.Throws<ArgumentNullException>(() => router.Register("bad", 10, null!, static (_, _) => "bad"));
        ScenarioExpect.Throws<ArgumentNullException>(() => router.Register("bad", 10, static (_, _) => true, null!));
        ScenarioExpect.Throws<ArgumentException>(() => router.Unregister(""));
    }

    private sealed record Order(string Id, decimal Total);
}
