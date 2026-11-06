using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.EventSourcing;
using PatternKit.Generators.EventSourcing;

namespace PatternKit.Examples.EventSourcingDemo;

public static class OrderEventSourcingDemo
{
    public static async ValueTask<OrderEventSourcingSummary> RunFluentAsync()
    {
        var store = OrderEventSourcingPolicies.CreateFluentStore();
        return await RunScenarioAsync(store, "order-100");
    }

    public static async ValueTask<OrderEventSourcingSummary> RunGeneratedAsync()
        => await RunScenarioAsync(GeneratedOrderEventStore.CreateStore(), "order-200");

    private static async ValueTask<OrderEventSourcingSummary> RunScenarioAsync(IEventStore<OrderEvent, string> store, string orderId)
    {
        _ = await store.AppendAsync(orderId, 0, [
            new OrderPlaced(orderId, "customer-1", 125m, DateTimeOffset.UtcNow),
            new OrderPaid(orderId, "payment-1", DateTimeOffset.UtcNow)
        ]);
        var stream = await store.ReadStreamAsync(orderId);
        return OrderProjection.Project(store.Name, stream);
    }
}

public abstract record OrderEvent(string OrderId, DateTimeOffset OccurredAt);

public sealed record OrderPlaced(string OrderId, string CustomerId, decimal Total, DateTimeOffset OccurredAt)
    : OrderEvent(OrderId, OccurredAt);

public sealed record OrderPaid(string OrderId, string PaymentId, DateTimeOffset OccurredAt)
    : OrderEvent(OrderId, OccurredAt);

public sealed record OrderEventSourcingSummary(
    string StoreName,
    string OrderId,
    string CustomerId,
    decimal Total,
    bool Paid,
    long Version);

public static class OrderEventSourcingPolicies
{
    public static InMemoryEventStore<OrderEvent, string> CreateFluentStore()
        => InMemoryEventStore<OrderEvent, string>.Create("order-events").Build();
}

public static class OrderProjection
{
    public static OrderEventSourcingSummary Project(string storeName, IReadOnlyList<StoredEvent<OrderEvent, string>> stream)
    {
        var orderId = "";
        var customerId = "";
        var total = 0m;
        var paid = false;
        var version = 0L;

        foreach (var stored in stream.OrderBy(static entry => entry.Version))
        {
            version = stored.Version;
            switch (stored.Event)
            {
                case OrderPlaced placed:
                    orderId = placed.OrderId;
                    customerId = placed.CustomerId;
                    total = placed.Total;
                    break;
                case OrderPaid paidEvent:
                    orderId = paidEvent.OrderId;
                    paid = true;
                    break;
            }
        }

        return new(storeName, orderId, customerId, total, paid, version);
    }
}

public sealed class OrderEventSourcingWorkflow
{
    private readonly IEventStore<OrderEvent, string> _store;

    public OrderEventSourcingWorkflow(IEventStore<OrderEvent, string> store)
    {
        _store = store;
    }

    public async ValueTask<OrderEventSourcingSummary> PlaceAndPayAsync(
        string orderId,
        string customerId,
        decimal total,
        string paymentId,
        CancellationToken cancellationToken = default)
    {
        _ = await _store.AppendAsync(orderId, 0, [
            new OrderPlaced(orderId, customerId, total, DateTimeOffset.UtcNow),
            new OrderPaid(orderId, paymentId, DateTimeOffset.UtcNow)
        ], cancellationToken).ConfigureAwait(false);

        var stream = await _store.ReadStreamAsync(orderId, cancellationToken).ConfigureAwait(false);
        return OrderProjection.Project(_store.Name, stream);
    }
}

public sealed record OrderEventSourcingDemoRunner(
    Func<ValueTask<OrderEventSourcingSummary>> RunFluentAsync,
    Func<ValueTask<OrderEventSourcingSummary>> RunGeneratedAsync);

public static class OrderEventSourcingServiceCollectionExtensions
{
    public static IServiceCollection AddOrderEventSourcingDemo(this IServiceCollection services)
    {
        services.AddScoped<IEventStore<OrderEvent, string>>(_ => OrderEventSourcingPolicies.CreateFluentStore());
        services.AddScoped<OrderEventSourcingWorkflow>();
        services.AddSingleton(new OrderEventSourcingDemoRunner(
            OrderEventSourcingDemo.RunFluentAsync,
            OrderEventSourcingDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateEventStore(typeof(OrderEvent), typeof(string), FactoryName = "CreateStore", StoreName = "order-events")]
public static partial class GeneratedOrderEventStore;
