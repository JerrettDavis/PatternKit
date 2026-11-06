using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.DomainEvents;
using PatternKit.Generators.DomainEvents;

namespace PatternKit.Examples.DomainEventDemo;

public static class OrderDomainEventDemo
{
    public static async ValueTask<OrderDomainEventSummary> RunFluentAsync()
    {
        var projection = new OrderEventProjection();
        var audit = new List<string>();
        var dispatcher = OrderDomainEventPolicies.CreateFluentDispatcher(projection, audit);
        var placed = new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, "order-100", "customer-1", 125m);
        var result = await dispatcher.DispatchAsync(placed);
        return new(result.Succeeded, projection.OrderIds.ToArray(), audit.ToArray());
    }

    public static async ValueTask<OrderDomainEventSummary> RunGeneratedAsync()
    {
        GeneratedOrderDomainEvents.Projection = new OrderEventProjection();
        GeneratedOrderDomainEvents.Audit = new List<string>();
        var placed = new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, "order-200", "customer-2", 75m);
        var result = await GeneratedOrderDomainEvents.CreateDispatcher().DispatchAsync(placed);
        return new(result.Succeeded, GeneratedOrderDomainEvents.Projection.OrderIds.ToArray(), GeneratedOrderDomainEvents.Audit.ToArray());
    }
}

public abstract record OrderDomainEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId, string CustomerId, decimal Total)
    : OrderDomainEvent(EventId, OccurredAt);

public sealed record OrderBilled(Guid EventId, DateTimeOffset OccurredAt, string OrderId, decimal Total)
    : OrderDomainEvent(EventId, OccurredAt);

public sealed record OrderDomainEventSummary(bool Dispatched, IReadOnlyList<string> ProjectedOrderIds, IReadOnlyList<string> AuditEntries);

public sealed class OrderEventProjection
{
    private readonly List<string> _orderIds = new();

    public IReadOnlyList<string> OrderIds => _orderIds;

    public void Apply(OrderPlaced domainEvent)
        => _orderIds.Add(domainEvent.OrderId);
}

public static class OrderDomainEventPolicies
{
    public static DomainEventDispatcher<OrderDomainEvent> CreateFluentDispatcher(OrderEventProjection projection, List<string> audit)
    {
        if (projection is null)
            throw new ArgumentNullException(nameof(projection));
        if (audit is null)
            throw new ArgumentNullException(nameof(audit));

        return DomainEventDispatcher<OrderDomainEvent>.Create("order-domain-events")
            .Handle<OrderPlaced>((domainEvent, _) =>
            {
                projection.Apply(domainEvent);
                return ValueTask.CompletedTask;
            })
            .Handle<OrderPlaced>((domainEvent, _) =>
            {
                audit.Add($"placed:{domainEvent.OrderId}:{domainEvent.Total}");
                return ValueTask.CompletedTask;
            })
            .Handle<OrderBilled>((domainEvent, _) =>
            {
                audit.Add($"billed:{domainEvent.OrderId}:{domainEvent.Total}");
                return ValueTask.CompletedTask;
            })
            .Build();
    }
}

public sealed class OrderDomainEventWorkflow
{
    private readonly IDomainEventDispatcher<OrderDomainEvent> _dispatcher;
    private readonly OrderEventProjection _projection;
    private readonly List<string> _audit;

    public OrderDomainEventWorkflow(IDomainEventDispatcher<OrderDomainEvent> dispatcher, OrderEventProjection projection, List<string> audit)
    {
        _dispatcher = dispatcher;
        _projection = projection;
        _audit = audit;
    }

    public async ValueTask<OrderDomainEventSummary> PlaceAsync(string orderId, string customerId, decimal total, CancellationToken cancellationToken = default)
    {
        var placed = new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, orderId, customerId, total);
        var result = await _dispatcher.DispatchAsync(placed, cancellationToken).ConfigureAwait(false);
        return new(result.Succeeded, _projection.OrderIds.ToArray(), _audit.ToArray());
    }
}

public sealed record OrderDomainEventDemoRunner(
    Func<ValueTask<OrderDomainEventSummary>> RunFluentAsync,
    Func<ValueTask<OrderDomainEventSummary>> RunGeneratedAsync);

public static class OrderDomainEventServiceCollectionExtensions
{
    public static IServiceCollection AddOrderDomainEventDemo(this IServiceCollection services)
    {
        services.AddScoped<OrderEventProjection>();
        services.AddScoped<List<string>>();
        services.AddScoped<IDomainEventDispatcher<OrderDomainEvent>>(sp =>
            OrderDomainEventPolicies.CreateFluentDispatcher(
                sp.GetRequiredService<OrderEventProjection>(),
                sp.GetRequiredService<List<string>>()));
        services.AddScoped<OrderDomainEventWorkflow>();
        services.AddSingleton(new OrderDomainEventDemoRunner(
            OrderDomainEventDemo.RunFluentAsync,
            OrderDomainEventDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateDomainEventDispatcher(typeof(OrderDomainEvent), FactoryName = "CreateDispatcher", DispatcherName = "order-domain-events")]
public static partial class GeneratedOrderDomainEvents
{
    public static OrderEventProjection Projection { get; set; } = new();

    public static List<string> Audit { get; set; } = new();

    [DomainEventHandler(typeof(OrderPlaced), 10)]
    private static ValueTask Project(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        Projection.Apply(domainEvent);
        return ValueTask.CompletedTask;
    }

    [DomainEventHandler(typeof(OrderPlaced), 20)]
    private static ValueTask AuditPlaced(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        Audit.Add($"placed:{domainEvent.OrderId}:{domainEvent.Total}");
        return ValueTask.CompletedTask;
    }

    [DomainEventHandler(typeof(OrderBilled), 30)]
    private static ValueTask AuditBilled(OrderBilled domainEvent, CancellationToken cancellationToken)
    {
        Audit.Add($"billed:{domainEvent.OrderId}:{domainEvent.Total}");
        return ValueTask.CompletedTask;
    }
}
