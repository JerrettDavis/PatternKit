# Order Message Bus Example

The order message bus example shows a production-oriented topic bus with standard dependency injection:

- fluent `OrderMessageBuses.Create(...)` construction from application-owned channels;
- source-generated `GeneratedOrderMessageBus.Create()` topology;
- `AddOrderMessageBusDemo()` for `IServiceCollection` integration;
- TinyBDD coverage for fluent behavior, generated parity, direct DI registration, and aggregate `AddPatternKitExamples()` import.

```csharp
var services = new ServiceCollection();
services.AddOrderMessageBusDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var runner = provider.GetRequiredService<OrderMessageBusExampleRunner>();
var summary = runner.RunGenerated(orderEvents);
```

The service publishes accepted orders to fulfillment and audit subscribers, while paid orders flow to billing and audit subscribers.
