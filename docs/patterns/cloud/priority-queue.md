# Priority Queue

Priority Queue schedules queued work by business priority instead of arrival order alone.

```csharp
var queue = PriorityQueuePolicy<FulfillmentPriorityWork, int>
    .Create("fulfillment-priority")
    .WithPrioritySelector(work => work.Expedited ? 10 : 1)
    .DequeueHighestPriorityFirst()
    .Build();

queue.Enqueue(new FulfillmentPriorityWork("order-100", "enterprise", expedited: false));
var next = queue.Dequeue();
```

Use it when high-value, urgent, or otherwise prioritized work should be processed ahead of normal work while preserving FIFO ordering for matching priorities. The fluent API supports custom comparers and highest-first or lowest-first ordering.

The source-generated path uses `[GeneratePriorityQueue]` and `[PriorityQueuePrioritySelector]`. Import the fulfillment example through `AddFulfillmentPriorityQueueDemo()` or `AddPatternKitExamples()`.
