# Materialized View Generator

`GenerateMaterializedViewAttribute` creates a typed `MaterializedView<TState,TEvent>` factory from annotated projection handler methods.

```csharp
[GenerateMaterializedView(typeof(OrderReadModel), typeof(OrderEvent), FactoryName = "CreateView", ViewName = "order-read-model")]
public static partial class GeneratedOrderMaterializedView
{
    [MaterializedViewHandler(typeof(OrderPlaced), Order = 10)]
    private static OrderReadModel ApplyPlaced(OrderReadModel state, OrderPlaced @event)
        => state with { OrderId = @event.OrderId };
}
```

The generated factory is equivalent to:

```csharp
MaterializedView<OrderReadModel, OrderEvent>
    .Create("order-read-model")
    .WithHandler<OrderPlaced>(ApplyPlaced, 10)
    .Build();
```

Diagnostics:

- `PKMV001`: host type must be partial.
- `PKMV002`: at least one `[MaterializedViewHandler]` method is required.
- `PKMV003`: handler methods must be static and return the state type, or `ValueTask<TState>` with a `CancellationToken`.
