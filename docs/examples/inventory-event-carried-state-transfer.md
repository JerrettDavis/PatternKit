# Inventory Event-Carried State Transfer

The inventory event-carried state transfer example projects `InventoryAdjustedEvent` payloads into an importable inventory read model service.

```csharp
services.AddInventoryEventCarriedStateTransferDemo();

var runner = provider.GetRequiredService<InventoryEventCarriedStateTransferDemoRunner>();
var summary = runner.RunGenerated(new InventoryAdjustedEvent("SKU-100", 12, "CHI-01", 4));
```

The example includes fluent and source-generated construction plus an `IServiceCollection` extension for standard .NET hosts.
