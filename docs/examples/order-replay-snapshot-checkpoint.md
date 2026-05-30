# Order Replay Snapshot Checkpoint Management

This example models an event-sourced order replay service that stores compact checkpoints after rebuilding order state. A replay can resume from a usable checkpoint and only apply later events.

The fluent path builds the checkpoint manager directly:

```csharp
var manager = OrderReplaySnapshotCheckpointPolicies.CreateFluentManager();
```

The generated path uses a source-generated factory:

```csharp
var manager = GeneratedOrderReplayCheckpoints.CreateManager();
```

The example is importable through standard dependency injection:

```csharp
services.AddOrderReplaySnapshotCheckpointDemo();
```

`OrderReplayService` composes an `IEventStore<OrderReplayEvent, string>` with `SnapshotCheckpointManager<string, OrderReplaySnapshot>`. Production applications can replace the in-memory event store with their own storage while keeping the checkpoint manager construction, stale checkpoint handling, and replay tests intact.
