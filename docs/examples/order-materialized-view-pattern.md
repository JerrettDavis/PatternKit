# Order Materialized View Pattern

This production-shaped example builds an order read model from placed, paid, and shipped events.

It demonstrates:

- fluent `MaterializedView<OrderReadModel,OrderReadModelEvent>` construction
- generated materialized view factory with `[GenerateMaterializedView]`
- deterministic projection handler ordering
- scoped `IMaterializedView<OrderReadModel,OrderReadModelEvent>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddOrderMaterializedViewDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderMaterializedViewWorkflow>();
var summary = await workflow.BuildReadModelAsync(events);
```

The registered view is scoped so importing applications can compose projection workflows with event stores, inboxes, hosted consumers, tenant services, or ASP.NET Core request services.

Files:

- `src/PatternKit.Examples/MaterializedViewDemo/OrderMaterializedViewDemo.cs`
- `test/PatternKit.Examples.Tests/MaterializedViewDemo/OrderMaterializedViewDemoTests.cs`
