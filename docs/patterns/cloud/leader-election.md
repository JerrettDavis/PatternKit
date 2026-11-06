# Leader Election

Leader Election coordinates one active worker among several candidates by issuing renewable leases.

```csharp
var election = LeaderElection<WarehouseWorkerContext>
    .Create("warehouse-replenishment-leader")
    .LeaseDuration(TimeSpan.FromSeconds(30))
    .Build();

var candidate = LeaderElectionCandidate.Create("warehouse-node-a", context)
    .OnAcquired((lease, ctx) => ctx.Log.Add($"acquired:{lease.Term}"))
    .OnRenewed((lease, ctx) => ctx.Log.Add($"renewed:{lease.Term}"))
    .OnReleased(ctx => ctx.Log.Add("released"))
    .Build();

var result = election.TryAcquire(candidate);
```

Use it when a hosted service, scheduler, projection worker, or queue processor must have only one active instance while other nodes remain ready to take over after lease expiry. The runtime path exposes acquisition, renewal, release, contention, and expiry as explicit result states.

The source-generated path uses `[GenerateLeaderElection]`, `[LeaderCandidateId]`, `[LeaderAcquired]`, `[LeaderRenewed]`, and `[LeaderReleased]`. Import the example through `AddWarehouseLeaderElectionDemo()` or `AddPatternKitExamples()`.
