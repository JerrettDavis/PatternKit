# Distributed Lock Generator

`[GenerateDistributedLock]` creates a typed `DistributedLock<TKey>` factory with configured lock name and lease duration.

```csharp
[GenerateDistributedLock(typeof(string), LockName = "order-allocation-lock", LeaseDurationMilliseconds = 30000)]
public static partial class OrderAllocationLocks;

var mutex = OrderAllocationLocks.Create();
```

Diagnostics:

- `PKDLOCK001`: host type must be partial.
- `PKDLOCK002`: factory name, lock name, and lease duration must be valid.
