# Snapshot / Checkpoint Management Generator

The Snapshot / Checkpoint Management generator creates a strongly typed factory for a `SnapshotCheckpointManager<TKey, TSnapshot>` from a partial host type.

```csharp
using PatternKit.Generators.SnapshotCheckpoints;

[GenerateSnapshotCheckpointManager(
    typeof(string),
    typeof(OrderReplaySnapshot),
    FactoryMethodName = "CreateManager",
    ManagerName = "order-replay-checkpoints")]
public static partial class GeneratedOrderReplayCheckpoints;
```

Generated output:

```csharp
SnapshotCheckpointManager<string, OrderReplaySnapshot> manager =
    GeneratedOrderReplayCheckpoints.CreateManager();
```

Use the fluent manager when runtime configuration needs a custom comparer, clock, or stale write policy. Use the generated factory when a module wants a discoverable, allocation-light construction path with the manager name and types fixed at compile time.

## Diagnostics

- `PKSCP001`: the snapshot checkpoint manager host type must be partial.
- `PKSCP002`: `FactoryMethodName` and `ManagerName` must be non-empty.
