# Snapshot / Checkpoint Management

Snapshot / Checkpoint Management stores compact replay state for long-running processors, event-sourced streams, projections, and resumable imports. It lets a processor resume from a known version instead of replaying the entire history on every run.

`SnapshotCheckpointManager<TKey, TSnapshot>` provides the fluent runtime path:

```csharp
var checkpoints = SnapshotCheckpointManager<string, OrderReplaySnapshot>
    .Create("order-replay-checkpoints")
    .UseComparer(StringComparer.OrdinalIgnoreCase)
    .Build();

checkpoints.Save("ORDER-100", 42, snapshot, "replay:ORDER-100");

var load = checkpoints.Load("ORDER-100", minimumVersion: 40);
if (load.IsUsable)
{
    var resumeFrom = load.Checkpoint!.Version;
}
```

The manager reports missing and stale checkpoints separately, rejects stale writes by default, and can be configured to overwrite stale writes when a caller owns that policy.

## Use When

- Replaying event streams or rebuilding projections should resume from compacted state.
- Processors need clear missing, found, and stale checkpoint outcomes.
- A checkpoint store should be importable through `IServiceCollection` and tested independently from storage infrastructure.

## Compare With

- Use Event Sourcing for append-only facts and Snapshot / Checkpoint Management for compact replay state.
- Use Materialized View when the snapshot is the user-facing read model.
- Use Inbox or Outbox when the concern is message idempotency or reliable publication.
