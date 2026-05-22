# Inventory Ambassador

The inventory ambassador example wraps outbound availability calls with SKU normalization, tenant connection policy, telemetry, and fallback cache behavior.

```csharp
services.AddInventoryAmbassadorDemo();

var runner = provider.GetRequiredService<InventoryAmbassadorDemoRunner>();
var availability = runner.RunGenerated("tenant-a", "sku-1");
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, and an ASP.NET Core minimal API mapping through `MapInventoryAmbassador()`.
