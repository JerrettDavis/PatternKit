# Priority Queue Generator

`[GeneratePriorityQueue]` creates a typed `PriorityQueuePolicy<TItem, TPriority>` factory from a priority selector method.

```csharp
[GeneratePriorityQueue(typeof(FulfillmentPriorityWork), typeof(int), FactoryMethodName = "Create", QueueName = "fulfillment-priority")]
public static partial class FulfillmentPriorityQueue
{
    [PriorityQueuePrioritySelector]
    private static int GetPriority(FulfillmentPriorityWork work) => work.Expedited ? 10 : 1;
}
```

The generated factory is parameterless, so applications can register it directly in `IServiceCollection` and inject the resulting queue into hosted workers or application services.

Diagnostics:

- `PKPQ001`: host type must be partial.
- `PKPQ002`: exactly one priority selector is required.
- `PKPQ003`: priority selector signature is invalid.
