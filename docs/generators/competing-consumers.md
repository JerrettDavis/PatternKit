# Competing Consumers Generator

`[GenerateCompetingConsumerGroup]` emits a strongly typed builder factory for a competing consumer group. The generated method configures the group name and concurrency limit; application code then registers the real consumers before building the group.

```csharp
[GenerateCompetingConsumerGroup(
    typeof(FulfillmentWork),
    typeof(FulfillmentResult),
    FactoryMethodName = "CreateGroup",
    GroupName = "fulfillment-consumers",
    MaxConcurrentDeliveries = 4)]
public static partial class FulfillmentConsumers;

var group = FulfillmentConsumers.CreateGroup()
    .AddConsumer("east-worker", (work, ct) => east.HandleAsync(work, ct))
    .AddConsumer("west-worker", (work, ct) => west.HandleAsync(work, ct))
    .Build();
```

Diagnostics:

- `PKCNS001`: the host type must be `partial`.
- `PKCNS002`: `MaxConcurrentDeliveries` must be at least one.
