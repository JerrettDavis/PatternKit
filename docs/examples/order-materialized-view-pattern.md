# Order Materialized View Pattern

This production-shaped example builds an order read model from placed, paid, and shipped events.

It demonstrates:

- fluent `MaterializedView<OrderReadModel,OrderReadModelEvent>` construction
- generated materialized view factory with `[GenerateMaterializedView]`
- deterministic projection handler ordering
- singleton `IMaterializedView<OrderReadModel,OrderReadModelEvent>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddOrderMaterializedViewDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderMaterializedViewWorkflow>();
var summary = await workflow.BuildReadModelAsync(events);
```

The registered view is singleton because it is stateless; importing applications can register consuming projection workflows with the lifetime that matches their event store, inbox, hosted consumer, tenant, or ASP.NET Core request services.

Files:

- `src/PatternKit.Examples/MaterializedViewDemo/OrderMaterializedViewDemo.cs`
- `test/PatternKit.Examples.Tests/MaterializedViewDemo/OrderMaterializedViewDemoTests.cs`
