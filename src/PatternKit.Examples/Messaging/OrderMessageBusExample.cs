using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Examples.Messaging;

public sealed record BusOrderEvent(string OrderId, string Status, decimal Total);

public sealed record OrderMessageBusSummary(int FulfillmentCount, int BillingCount, int AuditCount, IReadOnlyList<string> Topics);

public sealed record OrderMessageBusChannels(
    MessageChannel<BusOrderEvent> Fulfillment,
    MessageChannel<BusOrderEvent> Billing,
    MessageChannel<BusOrderEvent> Audit);

public sealed class OrderMessageBusService(
    MessageBus<BusOrderEvent> bus,
    OrderMessageBusChannels channels)
{
    public OrderMessageBusSummary Publish(IEnumerable<BusOrderEvent> events)
    {
        foreach (var orderEvent in events)
        {
            var topic = orderEvent.Status == "paid" ? "paid" : "accepted";
            bus.Publish(topic, Message<BusOrderEvent>.Create(orderEvent).WithCorrelationId(orderEvent.OrderId));
        }

        return new(channels.Fulfillment.Count, channels.Billing.Count, channels.Audit.Count, bus.Topics);
    }
}

public static class OrderMessageBuses
{
    public static MessageBus<BusOrderEvent> Create(
        MessageChannel<BusOrderEvent> fulfillment,
        MessageChannel<BusOrderEvent> billing,
        MessageChannel<BusOrderEvent> audit)
        => MessageBus<BusOrderEvent>.Create("order-bus")
            .Route("accepted", fulfillment)
            .Route("accepted", audit)
            .Route("paid", billing)
            .Route("paid", audit)
            .Build();
}

[GenerateMessageBus(typeof(BusOrderEvent), FactoryName = "Create", BusName = "order-bus")]
public static partial class GeneratedOrderMessageBus
{
    private static readonly MessageChannel<BusOrderEvent> Fulfillment = MessageChannel<BusOrderEvent>.Create("fulfillment-orders").Build();
    private static readonly MessageChannel<BusOrderEvent> Billing = MessageChannel<BusOrderEvent>.Create("billing-orders").Build();
    private static readonly MessageChannel<BusOrderEvent> Audit = MessageChannel<BusOrderEvent>.Create("order-audit").Build();

    [MessageBusRoute("accepted")]
    private static MessageChannel<BusOrderEvent> AcceptedFulfillment() => Fulfillment;

    [MessageBusRoute("accepted")]
    private static MessageChannel<BusOrderEvent> AcceptedAudit() => Audit;

    [MessageBusRoute("paid")]
    private static MessageChannel<BusOrderEvent> PaidBilling() => Billing;

    [MessageBusRoute("paid")]
    private static MessageChannel<BusOrderEvent> PaidAudit() => Audit;

    public static (MessageChannel<BusOrderEvent> Fulfillment, MessageChannel<BusOrderEvent> Billing, MessageChannel<BusOrderEvent> Audit) Channels()
        => (Fulfillment, Billing, Audit);
}

public sealed class OrderMessageBusExampleRunner(OrderMessageBusService service)
{
    public OrderMessageBusSummary RunGenerated(IEnumerable<BusOrderEvent> events) => service.Publish(events);

    public static OrderMessageBusSummary RunFluent(IEnumerable<BusOrderEvent> events)
    {
        var fulfillment = MessageChannel<BusOrderEvent>.Create("fulfillment-orders").Build();
        var billing = MessageChannel<BusOrderEvent>.Create("billing-orders").Build();
        var audit = MessageChannel<BusOrderEvent>.Create("order-audit").Build();
        var channels = new OrderMessageBusChannels(fulfillment, billing, audit);
        return new OrderMessageBusService(OrderMessageBuses.Create(fulfillment, billing, audit), channels).Publish(events);
    }

    public static OrderMessageBusSummary RunGeneratedStatic(IEnumerable<BusOrderEvent> events)
    {
        var channels = GeneratedOrderMessageBus.Channels();
        channels.Fulfillment.Drain();
        channels.Billing.Drain();
        channels.Audit.Drain();
        return new OrderMessageBusService(GeneratedOrderMessageBus.Create(), new(channels.Fulfillment, channels.Billing, channels.Audit)).Publish(events);
    }
}

public static class OrderMessageBusServiceCollectionExtensions
{
    public static IServiceCollection AddOrderMessageBusDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => new OrderMessageBusChannels(
            MessageChannel<BusOrderEvent>.Create("fulfillment-orders").Build(),
            MessageChannel<BusOrderEvent>.Create("billing-orders").Build(),
            MessageChannel<BusOrderEvent>.Create("order-audit").Build()));
        services.AddSingleton(sp => OrderMessageBuses.Create(
            sp.GetRequiredService<OrderMessageBusChannels>().Fulfillment,
            sp.GetRequiredService<OrderMessageBusChannels>().Billing,
            sp.GetRequiredService<OrderMessageBusChannels>().Audit));
        services.AddSingleton<OrderMessageBusService>();
        services.AddSingleton<OrderMessageBusExampleRunner>();
        return services;
    }
}
