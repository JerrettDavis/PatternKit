# Aggregate Root Generator

The Aggregate Root generator turns a decision method and event applier into a typed aggregate command handler factory.

## Usage

```csharp
using PatternKit.Generators.Aggregates;

[GenerateAggregateCommandHandler(
    typeof(OrderAggregate),
    typeof(OrderCommand),
    typeof(IOrderEvent),
    HandlerName = "orders")]
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

Generated output:

```csharp
var handler = OrderHandlers.Create();
var result = handler.Execute(order, command);
```

## Method Shape

Decision methods must be static and return `IEnumerable<TEvent>`:

```csharp
static IEnumerable<TEvent> Decide(TAggregate aggregate, TCommand command)
```

Event appliers must be static void methods:

```csharp
static void Apply(TAggregate aggregate, TEvent domainEvent)
```

## Diagnostics

| ID | Meaning |
|---|---|
| `PKAGG001` | Host type must be `partial`. |
| `PKAGG002` | Host type must declare exactly one `[AggregateDecision]` method. |
| `PKAGG003` | Host type must declare exactly one `[AggregateEventApplier]` method. |
| `PKAGG004` | Decision method signature is invalid. |
| `PKAGG005` | Event applier method signature is invalid. |
