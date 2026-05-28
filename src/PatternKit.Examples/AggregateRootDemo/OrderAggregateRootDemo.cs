using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.Aggregates;
using PatternKit.Generators.Aggregates;

namespace PatternKit.Examples.AggregateRootDemo;

/// <summary>
/// Production-style order aggregate root with fluent and source-generated command handlers.
/// </summary>
public static class OrderAggregateRootDemo
{
    public abstract record OrderCommand(string OrderId);

    public sealed record PlaceOrder(string OrderId, decimal Total) : OrderCommand(OrderId);

    public sealed record PayOrder(string OrderId) : OrderCommand(OrderId);

    public interface IOrderEvent
    {
        string OrderId { get; }
    }

    public sealed record OrderPlaced(string OrderId, decimal Total) : IOrderEvent;

    public sealed record OrderPaid(string OrderId) : IOrderEvent;

    public sealed class OrderAggregate(string id) : AggregateRoot<string, IOrderEvent>(id)
    {
        public decimal Total { get; private set; }

        public bool IsPlaced { get; private set; }

        public bool IsPaid { get; private set; }

        public void Record(IOrderEvent domainEvent) => Raise(domainEvent, Apply);

        public void Apply(IOrderEvent domainEvent)
        {
            switch (domainEvent)
            {
                case OrderPlaced placed:
                    IsPlaced = true;
                    Total = placed.Total;
                    break;
                case OrderPaid:
                    IsPaid = true;
                    break;
            }
        }
    }

    public sealed record OrderSummary(string OrderId, bool IsPlaced, bool IsPaid, decimal Total, long Version, IReadOnlyList<IOrderEvent> Changes);

    public static AggregateCommandHandler<OrderAggregate, OrderCommand, IOrderEvent> CreateFluentHandler()
        => AggregateCommandHandler<OrderAggregate, OrderCommand, IOrderEvent>.Create(
            "order-aggregate-fluent",
            Decide,
            static (aggregate, domainEvent) => aggregate.Record(domainEvent));

    public static AggregateCommandHandler<OrderAggregate, OrderCommand, IOrderEvent> CreateGeneratedHandler()
        => GeneratedOrderAggregateHandlers.Create();

    public static OrderSummary ExecuteOrder(AggregateCommandHandler<OrderAggregate, OrderCommand, IOrderEvent> handler)
    {
        var aggregate = new OrderAggregate("ORD-100");
        handler.Execute(aggregate, new PlaceOrder("ORD-100", 125m));
        handler.Execute(aggregate, new PayOrder("ORD-100"));

        return new OrderSummary(
            aggregate.Id,
            aggregate.IsPlaced,
            aggregate.IsPaid,
            aggregate.Total,
            aggregate.Version,
            aggregate.UncommittedEvents.ToArray());
    }

    public static IEnumerable<IOrderEvent> Decide(OrderAggregate aggregate, OrderCommand command)
    {
        switch (command)
        {
            case PlaceOrder place when !aggregate.IsPlaced && place.Total > 0m:
                return [new OrderPlaced(place.OrderId, place.Total)];
            case PayOrder pay when aggregate.IsPlaced && !aggregate.IsPaid:
                return [new OrderPaid(pay.OrderId)];
            default:
                return [];
        }
    }

    public static IServiceCollection AddOrderAggregateRootDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => CreateGeneratedHandler());
        services.AddSingleton<OrderAggregateRootService>();
        return services;
    }
}

public sealed class OrderAggregateRootService(AggregateCommandHandler<OrderAggregateRootDemo.OrderAggregate, OrderAggregateRootDemo.OrderCommand, OrderAggregateRootDemo.IOrderEvent> handler)
{
    public OrderAggregateRootDemo.OrderSummary Run()
        => OrderAggregateRootDemo.ExecuteOrder(handler);
}

[GenerateAggregateCommandHandler(
    typeof(OrderAggregateRootDemo.OrderAggregate),
    typeof(OrderAggregateRootDemo.OrderCommand),
    typeof(OrderAggregateRootDemo.IOrderEvent),
    HandlerName = "order-aggregate-generated")]
public static partial class GeneratedOrderAggregateHandlers
{
    [AggregateDecision]
    private static IEnumerable<OrderAggregateRootDemo.IOrderEvent> Decide(
        OrderAggregateRootDemo.OrderAggregate aggregate,
        OrderAggregateRootDemo.OrderCommand command)
        => OrderAggregateRootDemo.Decide(aggregate, command);

    [AggregateEventApplier]
    private static void Apply(
        OrderAggregateRootDemo.OrderAggregate aggregate,
        OrderAggregateRootDemo.IOrderEvent domainEvent)
        => aggregate.Record(domainEvent);
}
