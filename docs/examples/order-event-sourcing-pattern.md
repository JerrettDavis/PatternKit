# Order Event Sourcing Pattern

This production-shaped example shows an order workflow backed by append-only events and replayed projections.

It demonstrates:

- fluent `InMemoryEventStore<OrderEvent,string>` construction
- generated event store factory with `[GenerateEventStore]`
- optimistic concurrency through expected stream versions
- scoped `IEventStore<OrderEvent,string>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddOrderEventSourcingDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderEventSourcingWorkflow>();
var summary = await workflow.PlaceAndPayAsync("order-100", "customer-1", 125m, "payment-1");
```

The registered event store is scoped so importing applications can compose it with request-scoped storage adapters, database sessions, tenant services, or unit-of-work boundaries.

Files:

- `src/PatternKit.Examples/EventSourcingDemo/OrderEventSourcingDemo.cs`
- `test/PatternKit.Examples.Tests/EventSourcingDemo/OrderEventSourcingDemoTests.cs`
