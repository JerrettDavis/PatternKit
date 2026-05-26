using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "DynamicRouter")]
public class DynamicRouterBenchmarks
{
    private static readonly DynamicFulfillmentOrder[] Orders =
    [
        new("O-100", "central", 1_250m),
        new("O-101", "west", 100m),
        new("O-102", "central", 50m)
    ];

    [Benchmark(Baseline = true, Description = "Fluent: create dynamic router")]
    [BenchmarkCategory("Fluent", "Construction")]
    public DynamicRouter<DynamicFulfillmentOrder, FulfillmentRouteDecision> Fluent_CreateDynamicRouter()
        => OrderDynamicRouters.Create();

    [Benchmark(Description = "Generated: create dynamic router")]
    [BenchmarkCategory("Generated", "Construction")]
    public DynamicRouter<DynamicFulfillmentOrder, FulfillmentRouteDecision> Generated_CreateDynamicRouter()
        => GeneratedOrderDynamicRouter.Create();

    [Benchmark(Description = "Fluent: route fulfillment orders")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderDynamicRouterSummary Fluent_RouteFulfillmentOrders()
        => OrderDynamicRouterExampleRunner.RunFluent(Orders);

    [Benchmark(Description = "Generated: route fulfillment orders")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderDynamicRouterSummary Generated_RouteFulfillmentOrders()
        => OrderDynamicRouterExampleRunner.RunGeneratedStatic(Orders);
}
