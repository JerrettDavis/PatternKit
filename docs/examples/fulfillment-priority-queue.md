# Fulfillment Priority Queue

The fulfillment priority queue example schedules enterprise or expedited orders ahead of standard fulfillment work.

```csharp
services.AddFulfillmentPriorityQueueDemo();

var service = provider.GetRequiredService<FulfillmentPriorityQueueService>();
var summary = service.Schedule(
    new FulfillmentPriorityWork("order-standard", "standard", expedited: false),
    new FulfillmentPriorityWork("order-enterprise", "enterprise", expedited: false));
```

The example includes fluent and source-generated construction, stable ordering for matching priorities, and `IServiceCollection` registration for existing .NET applications.
