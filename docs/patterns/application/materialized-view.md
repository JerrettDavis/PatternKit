# Materialized View

Materialized View builds a query-optimized read model by replaying domain or integration events. Use it when command-side state is not shaped for screens, reports, APIs, or search endpoints.

PatternKit provides `IMaterializedView<TState,TEvent>` and `MaterializedView<TState,TEvent>` in `PatternKit.Application.MaterializedViews`.

```csharp
var view = MaterializedView<OrderReadModel, OrderEvent>
    .Create("order-read-model")
    .WithHandler<OrderPlaced>((state, e) => state with { OrderId = e.OrderId })
    .WithHandler<OrderPaid>((state, _) => state with { Status = "Paid" })
    .Build();

var projected = await view.ProjectAsync(OrderReadModel.Empty("order-read-model"), events);
```

Handlers are ordered, deterministic, cancellation-aware, and can be synchronous or asynchronous. Register `IMaterializedView<TState,TEvent>` through `IServiceCollection` when projection workflows need to compose with event stores, inboxes, hosted services, or ASP.NET Core request scopes.

Use the source-generated path when the read model and event handlers are stable.

See also:

- [Materialized View generator](../../generators/materialized-view.md)
- [Order Materialized View example](../../examples/order-materialized-view-pattern.md)
