# Aggregate Root

Aggregate Root protects a consistency boundary by deciding which domain events a command may produce and applying those events to the aggregate state.

Use it when updates must enforce invariants across a cluster of entities, such as order placement and payment, account lifecycle changes, or inventory reservations.

## Fluent Path

```csharp
using PatternKit.Application.Aggregates;

var handler = AggregateCommandHandler<OrderAggregate, OrderCommand, IOrderEvent>.Create(
    "order-aggregate",
    Decide,
    (aggregate, domainEvent) => aggregate.Record(domainEvent));

var result = handler.Execute(order, new PayOrder(order.Id));
```

`AggregateRoot<TId,TEvent>` tracks identity, version, and uncommitted events. `AggregateCommandHandler<TAggregate,TCommand,TEvent>` keeps command decision and event application explicit and testable.

## Generated Path

```csharp
using PatternKit.Generators.Aggregates;

[GenerateAggregateCommandHandler(typeof(OrderAggregate), typeof(OrderCommand), typeof(IOrderEvent))]
public static partial class OrderHandlers
{
    [AggregateDecision]
    private static IEnumerable<IOrderEvent> Decide(OrderAggregate aggregate, OrderCommand command)
        => OrderDecisions.Decide(aggregate, command);

    [AggregateEventApplier]
    private static void Apply(OrderAggregate aggregate, IOrderEvent domainEvent)
        => aggregate.Record(domainEvent);
}
```

The generator emits a factory returning `AggregateCommandHandler<TAggregate,TCommand,TEvent>` so application services can inject the generated command path.

## IoC Usage

```csharp
services.AddOrderAggregateRootDemo();
services.AddSingleton<OrderWorkflowService>();
```

The example in `docs/examples/order-aggregate-root-pattern.md` shows fluent and generated aggregate command handling through standard `IServiceCollection`.
