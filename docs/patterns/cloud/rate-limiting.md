# Rate Limiting

Rate Limiting protects a resource by allowing only a bounded number of operations for a tenant, user, route, or other key in a time window. PatternKit provides a fixed-window, key-partitioned policy for library and application code that needs deterministic throttling without taking a dependency on a transport stack.

PatternKit provides `RateLimitPolicy<TResult>` in `PatternKit.Cloud.RateLimiting`.

```csharp
var policy = RateLimitPolicy<SearchResponse>
    .Create("product-search")
    .WithPermitLimit(2)
    .WithWindow(TimeSpan.FromMinutes(1))
    .Build();

var result = await policy.ExecuteAsync(
    tenantId,
    ct => search.SearchAsync(tenantId, query, ct),
    cancellationToken);
```

The policy returns `RateLimitResult<TResult>` so callers can inspect whether the operation ran, the remaining permits for the current window, and the retry-after timestamp for rejected calls.

## Production Notes

- Partition by the same key that owns the budget, such as tenant, account, user, API route, or downstream dependency.
- Keep the protected operation inside the policy so rejected requests do not call the origin service.
- Use a short fixed window for simple application-local throttling and operational demos.
- Use `Reset` or `Clear` only for administrative control paths and tests.
- Replace process-local policies with a distributed limiter when multiple app instances must share the same budget.

The product search rate limiting example shows both fluent and source-generated policy creation, plus `IServiceCollection` registration for importing applications.
