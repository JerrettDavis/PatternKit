# Distributed Lock / Lease

Distributed Lock / Lease coordinates exclusive ownership of a resource through expiring tokens. Use it when one worker, request, or host should mutate a resource while contenders wait or retry.

```csharp
var mutex = DistributedLock<string>
    .Create("order-allocation-lock")
    .LeaseDuration(TimeSpan.FromSeconds(30))
    .Build();

var acquired = mutex.TryAcquire("ORDER-100", "allocator-a");
var renewed = mutex.Renew(acquired.Lease!);
var released = mutex.Release(renewed.Lease!);
```

The fluent path exposes acquisition, contention, renewal, expiry, release, snapshots, and blocked state without requiring a container. The source-generated path uses `[GenerateDistributedLock]` to create a configured `DistributedLock<TKey>` factory for repeatable application composition.

Import the production-shaped example through `AddOrderAllocationDistributedLockDemo()` or the aggregate `AddPatternKitExamples()` registration.
