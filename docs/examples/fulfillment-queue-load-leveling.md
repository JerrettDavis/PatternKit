# Fulfillment Queue Load Leveling

This production-shaped example smooths fulfillment work with a queue load leveling policy before invoking a worker service.

It demonstrates:

- fluent `QueueLoadLevelingPolicy<FulfillmentQueueResult>` construction
- generated queue load leveling policy with `[GenerateQueueLoadLevelingPolicy]`
- bounded queue behavior for fulfillment bursts
- singleton policy and worker registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddFulfillmentQueueLoadLevelingDemo();

using var provider = services.BuildServiceProvider();
var service = provider.GetRequiredService<FulfillmentQueueLoadLevelingService>();

var result = await service.EnqueueAsync(new FulfillmentWorkItem("order-100", "central"));
```

Files:

- `src/PatternKit.Examples/QueueLoadLevelingDemo/FulfillmentQueueLoadLevelingDemo.cs`
- `test/PatternKit.Examples.Tests/QueueLoadLevelingDemo/FulfillmentQueueLoadLevelingDemoTests.cs`
