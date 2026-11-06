# Order Domain Event Pattern

This production-shaped example shows order events dispatched to projection and audit handlers.

It demonstrates:

- fluent `DomainEventDispatcher<OrderDomainEvent>` construction
- generated dispatcher factory with `[GenerateDomainEventDispatcher]`
- multiple ordered handlers for the same domain event
- scoped `IDomainEventDispatcher<OrderDomainEvent>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddOrderDomainEventDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderDomainEventWorkflow>();
var summary = await workflow.PlaceAsync("order-100", "customer-1", 125m);
```

The registered dispatcher is scoped so importing applications can safely compose event handlers with projections, audit stores, unit-of-work state, database sessions, tenant services, or ASP.NET Core request services.

Files:

- `src/PatternKit.Examples/DomainEventDemo/OrderDomainEventDemo.cs`
- `test/PatternKit.Examples.Tests/DomainEventDemo/OrderDomainEventDemoTests.cs`
