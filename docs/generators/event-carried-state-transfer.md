# Event-Carried State Transfer Generator

`[GenerateEventCarriedStateTransfer]` creates a typed `EventCarriedStateTransfer<TEvent, TKey, TState>` factory from key, version, and state mapper methods.

```csharp
[GenerateEventCarriedStateTransfer(typeof(InventoryAdjustedEvent), typeof(string), typeof(InventoryReadModel), TransferName = "inventory-state")]
public static partial class InventoryStateTransfer
{
    [EventCarriedStateKey]
    private static string Key(InventoryAdjustedEvent evt) => evt.Sku;

    [EventCarriedStateVersion]
    private static long Version(InventoryAdjustedEvent evt) => evt.Version;

    [EventCarriedStateMapper]
    private static InventoryReadModel Map(InventoryAdjustedEvent evt) => new(evt.Sku, evt.QuantityOnHand, evt.Warehouse);
}
```

The generated factory is parameterless and can be registered directly with `IServiceCollection`.

Diagnostics:

- `PKECST001`: host type must be partial.
- `PKECST002`: exactly one key selector, version selector, and state mapper are required.
- `PKECST003`: selector or mapper signature is invalid.
