# Inventory Message Channel

The inventory message channel example queues inventory adjustments and lets an application service explicitly process the next available message.

```csharp
services.AddInventoryMessageChannelDemo();

var service = provider.GetRequiredService<InventoryMessageChannelService>();
service.Enqueue(new InventoryAdjustment("SKU-100", 3, "cycle-count"));
var processed = service.TryProcessNext();
```

The example includes fluent and source-generated construction, bounded channel configuration, and `IServiceCollection` registration for existing .NET applications.
