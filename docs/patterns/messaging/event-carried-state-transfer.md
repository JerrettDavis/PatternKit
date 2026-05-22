# Event-Carried State Transfer

Event-Carried State Transfer publishes enough state in an event for subscribers to update their local model without calling back into the source service.

```csharp
var transfer = EventCarriedStateTransfer<InventoryAdjustedEvent, string, InventoryReadModel>
    .Create("inventory-state")
    .WithKey(evt => evt.Sku)
    .WithVersion(evt => evt.Version)
    .WithState(evt => new InventoryReadModel(evt.Sku, evt.QuantityOnHand, evt.Warehouse))
    .Build();

var carried = transfer.Transfer(inventoryAdjusted);
```

Use it when downstream services own read models, caches, or projections that should move forward from the event stream itself. The runtime path returns explicit transfer failures for selector or mapper errors.

The source-generated path uses `[GenerateEventCarriedStateTransfer]`, `[EventCarriedStateKey]`, `[EventCarriedStateVersion]`, and `[EventCarriedStateMapper]`. Import the example through `AddInventoryEventCarriedStateTransferDemo()` or `AddPatternKitExamples()`.
