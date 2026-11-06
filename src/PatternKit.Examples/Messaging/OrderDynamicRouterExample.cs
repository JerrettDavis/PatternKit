using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

public sealed record DynamicFulfillmentOrder(string OrderId, string Region, decimal Total);

public sealed record FulfillmentRouteDecision(string Route, string WorkQueue);

public sealed record OrderDynamicRouterSummary(IReadOnlyList<FulfillmentRouteDecision> Decisions, IReadOnlyList<string> ActiveRoutes);

public sealed class FulfillmentRoutingService(DynamicRouter<DynamicFulfillmentOrder, FulfillmentRouteDecision> router)
{
    public OrderDynamicRouterSummary Route(IEnumerable<DynamicFulfillmentOrder> orders)
    {
        var decisions = orders
            .Select(order => router.Route(Message<DynamicFulfillmentOrder>.Create(order).WithCorrelationId(order.OrderId)))
            .ToArray();

        return new(decisions, router.RouteNames);
    }

    public void ReplaceRegionalRoute(string region, string workQueue)
        => router.Register(
            $"region:{region}",
            5,
            (message, _) => string.Equals(message.Payload.Region, region, StringComparison.OrdinalIgnoreCase),
            (_, _) => new FulfillmentRouteDecision($"region:{region}", workQueue));
}

public static class OrderDynamicRouters
{
    public static DynamicRouter<DynamicFulfillmentOrder, FulfillmentRouteDecision> Create()
        => DynamicRouter<DynamicFulfillmentOrder, FulfillmentRouteDecision>.Create()
            .When("vip", 1, static (message, _) => message.Payload.Total >= 1_000m)
            .Then(static (_, _) => new FulfillmentRouteDecision("vip", "white-glove"))
            .When("west", 10, static (message, _) => message.Payload.Region == "west")
            .Then(static (_, _) => new FulfillmentRouteDecision("west", "west-fulfillment"))
            .Default(static (_, _) => new FulfillmentRouteDecision("default", "standard-fulfillment"))
            .Build();
}

[GenerateDynamicRouter(typeof(DynamicFulfillmentOrder), typeof(FulfillmentRouteDecision), FactoryName = "Create")]
public static partial class GeneratedOrderDynamicRouter
{
    private static bool IsVip(Message<DynamicFulfillmentOrder> message, MessageContext context)
        => message.Payload.Total >= 1_000m;

    private static bool IsWest(Message<DynamicFulfillmentOrder> message, MessageContext context)
        => message.Payload.Region == "west";

    [DynamicRoute("vip", 1, nameof(IsVip))]
    private static FulfillmentRouteDecision Vip(Message<DynamicFulfillmentOrder> message, MessageContext context)
        => new("vip", "white-glove");

    [DynamicRoute("west", 10, nameof(IsWest))]
    private static FulfillmentRouteDecision West(Message<DynamicFulfillmentOrder> message, MessageContext context)
        => new("west", "west-fulfillment");

    [DynamicRouteDefault]
    private static FulfillmentRouteDecision Default(Message<DynamicFulfillmentOrder> message, MessageContext context)
        => new("default", "standard-fulfillment");
}

public sealed class OrderDynamicRouterExampleRunner(FulfillmentRoutingService service)
{
    public OrderDynamicRouterSummary RunGenerated(IEnumerable<DynamicFulfillmentOrder> orders) => service.Route(orders);

    public static OrderDynamicRouterSummary RunFluent(IEnumerable<DynamicFulfillmentOrder> orders)
        => new FulfillmentRoutingService(OrderDynamicRouters.Create()).Route(orders);

    public static OrderDynamicRouterSummary RunGeneratedStatic(IEnumerable<DynamicFulfillmentOrder> orders)
        => new FulfillmentRoutingService(GeneratedOrderDynamicRouter.Create()).Route(orders);
}

public static class OrderDynamicRouterExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderDynamicRouterDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedOrderDynamicRouter.Create());
        services.AddSingleton<FulfillmentRoutingService>();
        services.AddSingleton<OrderDynamicRouterExampleRunner>();
        return services;
    }
}
