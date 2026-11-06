# Warehouse Polling Consumer

The warehouse polling consumer example explicitly pulls replenishment requests from a message source.

```csharp
services.AddWarehousePollingConsumerDemo();

GeneratedWarehousePollingConsumer.Enqueue(new ReplenishmentRequest("SKU-100", 4));
var service = provider.GetRequiredService<WarehousePollingConsumerService>();
var summary = service.Poll();
```

The example includes fluent and source-generated construction, a pull-based service boundary, and `IServiceCollection` registration for existing .NET applications.
