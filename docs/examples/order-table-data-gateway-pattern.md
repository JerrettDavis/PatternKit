# Order Table Data Gateway Pattern

This production-shaped example shows a row-oriented order table gateway.

It demonstrates:

- fluent `InMemoryTableDataGateway<OrderTableRow,string>` construction
- generated gateway factory with `[GenerateTableDataGateway]`
- row insert, update, query, and delete behavior
- scoped `ITableDataGateway<OrderTableRow,string>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddOrderTableDataGatewayDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderTableGatewayWorkflow>();
var summary = await workflow.CloseAsync("order-100", "customer-1", 125m);
```

The registered gateway is scoped so importing applications can compose it with request-scoped storage adapters, database sessions, tenant services, or transaction boundaries.

Files:

- `src/PatternKit.Examples/TableDataGatewayDemo/OrderTableDataGatewayDemo.cs`
- `test/PatternKit.Examples.Tests/TableDataGatewayDemo/OrderTableDataGatewayDemoTests.cs`
