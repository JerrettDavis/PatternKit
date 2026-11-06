# Inventory Service Activator

The inventory service activator example turns an inbound reservation message into a container-owned application service call.

```csharp
services.AddInventoryServiceActivatorDemo();

var service = provider.GetRequiredService<InventoryServiceActivatorService>();
var summary = service.Reserve(new InventoryReservationRequest("SKU-100", 5));
```

The example includes fluent and source-generated construction, typed message activation, and `IServiceCollection` registration for existing .NET applications.
