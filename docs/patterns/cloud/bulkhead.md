# Bulkhead

Bulkhead protects a system by isolating a dependency behind a bounded number of concurrent calls. When the dependency is saturated, callers can queue for a short period or fail fast without consuming more application resources.

PatternKit provides `BulkheadPolicy<TResult>` in `PatternKit.Cloud.Bulkhead`.

```csharp
var policy = BulkheadPolicy<ShippingAllocation>
    .Create("shipping-allocation")
    .WithMaxConcurrency(4)
    .WithMaxQueueLength(16)
    .WithQueueTimeout(TimeSpan.FromMilliseconds(250))
    .Build();

var result = await policy.ExecuteAsync(
    ct => shippingAllocator.ReserveAsync("ORDER-100", ct),
    cancellationToken);
```

The policy returns `BulkheadResult<TResult>` so callers can inspect success, rejection, queue timeout, queued execution, and the resulting value.

## Production Notes

- Use bulkheads around external systems, limited infrastructure pools, and high-cost workflows.
- Keep `MaxConcurrency` aligned with the dependency's real capacity, not the caller's thread count.
- Use small queue limits and bounded queue timeouts to avoid hiding overload.
- Treat rejected and timed-out outcomes differently: rejection means no queue capacity; timeout means the caller waited but no slot opened.
- Pair async calls with cancellation so queued callers can leave promptly.

The shipping bulkhead example shows both fluent and source-generated policy creation, plus `IServiceCollection` registration for importing applications.
