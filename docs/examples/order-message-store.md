# Order Message Store

The order message-store example shows an importable audit/replay store for order events.

```csharp
services.AddOrderMessageStoreDemo();

var service = provider.GetRequiredService<OrderMessageStoreService>();
var summary = service.Record(
    new OrderMessageStoreEvent("ORDER-100", "Submitted", 125m, containsSensitiveData: false),
    "MSG-100",
    "CHECKOUT-100");
```

The example includes:

- a fluent `OrderMessageStores.CreateAuditStore()` path,
- a generated `GeneratedOrderMessageStore.Create()` path,
- a retention policy that refuses sensitive payloads,
- `IServiceCollection` registration for standard .NET hosts.
