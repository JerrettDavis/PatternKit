# Fulfillment Control Bus

The fulfillment control-bus example shows an importable operations surface for a message processor.

```csharp
services.AddFulfillmentControlBusDemo();

var service = provider.GetRequiredService<FulfillmentControlBusService>();
var summary = service.Execute(new FulfillmentControlCommand("pause", "processor-1"));
```

The example includes:

- a fluent `FulfillmentControlBuses.Create(state)` path,
- a generated `GeneratedFulfillmentControlBus.Create()` path,
- container-owned processor state that handlers update,
- pause, resume, and drain commands,
- `IServiceCollection` registration for standard .NET hosts.
