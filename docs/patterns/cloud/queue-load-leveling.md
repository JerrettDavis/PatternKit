# Queue-Based Load Leveling

Queue-Based Load Leveling smooths bursts by placing work behind a bounded queue and processing it with a fixed number of workers. Use it when callers can wait briefly instead of pushing every request directly into a constrained downstream system.

PatternKit provides `QueueLoadLevelingPolicy<TResult>` in `PatternKit.Cloud.QueueLoadLeveling`.

```csharp
var policy = QueueLoadLevelingPolicy<FulfillmentQueueResult>
    .Create("fulfillment-queue")
    .WithMaxConcurrentWorkers(2)
    .WithMaxQueueLength(32)
    .WithQueueTimeout(TimeSpan.FromMilliseconds(500))
    .Build();

var result = await policy.ExecuteAsync(ct => worker.ProcessAsync(item, ct));
```

The policy rejects work when the waiting queue is full and times out work that waits longer than the configured queue timeout. Register it as a singleton when the policy represents a shared queue boundary for a downstream service or hosted worker.

See also:

- [Queue Load Leveling generator](../../generators/queue-load-leveling.md)
- [Fulfillment Queue Load Leveling example](../../examples/fulfillment-queue-load-leveling.md)
