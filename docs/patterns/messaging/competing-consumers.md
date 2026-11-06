# Competing Consumers

Competing Consumers lets multiple handlers pull from the same logical work stream while PatternKit controls delivery concurrency and records which consumer handled each message.

```csharp
var group = CompetingConsumerGroup<OrderWork, OrderResult>
    .Create("fulfillment-consumers")
    .WithMaxConcurrentDeliveries(4)
    .AddConsumer("east-worker", (work, ct) => east.HandleAsync(work, ct))
    .AddConsumer("west-worker", (work, ct) => west.HandleAsync(work, ct))
    .Build();

var result = await group.DispatchAsync(new OrderWork("order-100", "east"));
```

Use `DispatchAsync` when work should wait for the next available delivery slot. Use `TryDispatchAsync` when the caller should receive a rejected result immediately while the group is saturated.

The builder validates group names, consumer names, consumer handlers, required consumers, and concurrency limits.
