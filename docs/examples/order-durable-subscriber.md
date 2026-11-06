# Order Durable Subscriber

The order durable subscriber example shows a shipment projection catching up from a message store while checkpointing the subscriber name and last delivered sequence.

The example includes:

- a fluent `OrderDurableSubscribers.Create(...)` path for apps that do not use source generators;
- a `[GenerateDurableSubscriber]` path for compile-time factory generation;
- `AddOrderDurableSubscriberDemo()` for standard `IServiceCollection` integration;
- TinyBDD coverage for fluent, generated, and aggregate `AddPatternKitExamples()` registration.

```csharp
services.AddOrderDurableSubscriberDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var runner = provider.GetRequiredService<OrderDurableSubscriberExampleRunner>();
var summary = runner.RunGenerated([
    new OrderShipmentEvent("order-1", "Packed", "central"),
    new OrderShipmentEvent("order-2", "Shipped", "central")
]);
```

Use this shape when a projection, reporting model, search index, or integration read model needs to restart safely after process downtime.
