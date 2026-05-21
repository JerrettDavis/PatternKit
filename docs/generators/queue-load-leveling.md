# Queue Load Leveling Generator

`GenerateQueueLoadLevelingPolicyAttribute` creates a typed queue load leveling policy factory.

```csharp
[GenerateQueueLoadLevelingPolicy(
    typeof(FulfillmentQueueResult),
    FactoryMethodName = "CreatePolicy",
    PolicyName = "fulfillment-queue",
    MaxConcurrentWorkers = 2,
    MaxQueueLength = 32,
    QueueTimeoutMilliseconds = 500)]
public static partial class GeneratedFulfillmentQueueLoadLevelingPolicy;
```

Diagnostics:

- `PKQL001`: host type must be partial.
- `PKQL002`: worker count, queue length, and timeout must be non-negative, with at least one worker.
