# Shipment Resequencer

The shipment resequencer example accepts out-of-order shipment events and releases them only when the next contiguous sequence is available.

```csharp
services.AddShipmentResequencerDemo();

var service = provider.GetRequiredService<ShipmentResequencerService>();
var summary = service.Record(new ShipmentEvent(1, "SHIP-100", "Packed"));
```

The example includes fluent and source-generated construction, duplicate and stale sequence handling through the runtime result, and `IServiceCollection` registration for existing .NET applications.
