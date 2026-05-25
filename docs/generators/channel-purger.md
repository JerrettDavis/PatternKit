# Channel Purger Generator

`[GenerateChannelPurger]` creates a strongly typed factory for `ChannelPurger<TPayload>`.

```csharp
[GenerateChannelPurger(
    typeof(InventoryMaintenanceCommand),
    FactoryName = "Create",
    PurgerName = "inventory-maintenance-purger")]
public static partial class GeneratedInventoryChannelPurger;
```

Generated shape:

```csharp
public static ChannelPurger<InventoryMaintenanceCommand> Create(
    MessageChannel<InventoryMaintenanceCommand> channel);
```

The host type must be partial. The generated factory is intentionally small so applications can compose it with container-owned channels, audit sinks, and hosted maintenance services.
