# Message Channel Generator

`[GenerateMessageChannel]` creates a typed `MessageChannel<TPayload>` factory.

```csharp
[GenerateMessageChannel(typeof(InventoryAdjustment), FactoryName = "Create", ChannelName = "inventory-adjustments", Capacity = 32)]
public static partial class InventoryChannel;
```

Set `Capacity` to a positive value for bounded channels, or leave it at `-1` for unbounded channels. `BackpressurePolicy` maps to `MessageChannelBackpressurePolicy` and defaults to `Reject`.

Diagnostics:

- `PKCHN001`: host type must be partial.
- `PKCHN002`: capacity must be `-1` or greater than zero.
