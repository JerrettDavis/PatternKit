using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderDynamicRouterExampleTests
{
    [Scenario("FluentDynamicRouter RoutesOrdersThroughCurrentTable")]
    [Fact]
    public void FluentDynamicRouter_RoutesOrdersThroughCurrentTable()
    {
        var summary = OrderDynamicRouterExampleRunner.RunFluent(CreateOrders());

        ScenarioExpect.Equal(["white-glove", "west-fulfillment", "standard-fulfillment"], summary.Decisions.Select(static decision => decision.WorkQueue).ToArray());
        ScenarioExpect.Equal(["vip", "west"], summary.ActiveRoutes);
    }

    [Scenario("GeneratedDynamicRouter MatchesFluentRouting")]
    [Fact]
    public void GeneratedDynamicRouter_MatchesFluentRouting()
    {
        var fluent = OrderDynamicRouterExampleRunner.RunFluent(CreateOrders());
        var generated = OrderDynamicRouterExampleRunner.RunGeneratedStatic(CreateOrders());

        ScenarioExpect.Equal(fluent.Decisions, generated.Decisions);
        ScenarioExpect.Equal(fluent.ActiveRoutes, generated.ActiveRoutes);
    }

    [Scenario("DynamicRouter CanReplaceRuntimeRoute")]
    [Fact]
    public void DynamicRouter_CanReplaceRuntimeRoute()
    {
        var service = new FulfillmentRoutingService(OrderDynamicRouters.Create());

        service.ReplaceRegionalRoute("west", "west-overflow");
        var summary = service.Route([new DynamicFulfillmentOrder("order-2", "west", 100m)]);

        ScenarioExpect.Equal("west-overflow", ScenarioExpect.Single(summary.Decisions).WorkQueue);
    }

    [Scenario("ServiceCollection ImportsDynamicRouterExample")]
    [Fact]
    public void ServiceCollection_ImportsDynamicRouterExample()
    {
        var services = new ServiceCollection();
        services.AddOrderDynamicRouterDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var router = provider.GetRequiredService<DynamicRouter<DynamicFulfillmentOrder, FulfillmentRouteDecision>>();
        var runner = provider.GetRequiredService<OrderDynamicRouterExampleRunner>();

        var summary = runner.RunGenerated(CreateOrders());

        ScenarioExpect.NotNull(router);
        ScenarioExpect.Equal("white-glove", summary.Decisions.First().WorkQueue);
    }

    [Scenario("AggregateServiceCollection ImportsDynamicRouterExample")]
    [Fact]
    public void AggregateServiceCollection_ImportsDynamicRouterExample()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<OrderDynamicRouterExampleService>();

        var summary = example.Service.Route(CreateOrders());

        ScenarioExpect.Equal(3, summary.Decisions.Count);
        ScenarioExpect.Contains("vip", summary.ActiveRoutes);
    }

    private static DynamicFulfillmentOrder[] CreateOrders()
        => [new("order-1", "central", 1_250m), new("order-2", "west", 100m), new("order-3", "central", 50m)];
}
