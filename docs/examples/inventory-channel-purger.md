# Inventory Channel Purger

The inventory channel purger example demonstrates an operational maintenance flow for clearing an in-memory inventory maintenance channel.

The example includes:

- A fluent `ChannelPurger<InventoryMaintenanceCommand>` with audit recording.
- A source-generated purger factory via `[GenerateChannelPurger]`.
- `AddInventoryChannelPurgerDemo()` for standard `IServiceCollection` integration.
- TinyBDD coverage for fluent and generated/container-backed paths.

```csharp
services.AddInventoryChannelPurgerDemo();

var runner = provider.GetRequiredService<InventoryChannelPurgerExampleRunner>();
var summary = runner.RunGenerated(commands);
```
