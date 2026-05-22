# Leader Election Generator

`[GenerateLeaderElection]` creates a typed `LeaderElection<TContext>` factory and a candidate factory from callback methods.

```csharp
[GenerateLeaderElection(typeof(WarehouseWorkerContext), ElectionName = "warehouse-replenishment-leader")]
public static partial class WarehouseLeader
{
    [LeaderCandidateId]
    private static string CandidateId(WarehouseWorkerContext context) => context.NodeId;

    [LeaderAcquired]
    private static void Acquired(LeaderLease lease, WarehouseWorkerContext context) { }
}
```

Diagnostics:

- `PKLE001`: host type must be partial.
- `PKLE002`: exactly one candidate id selector is required.
- `PKLE003`: candidate id or callback signature is invalid.
- `PKLE004`: lease duration must be positive.
