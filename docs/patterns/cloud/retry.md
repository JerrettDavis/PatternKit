# Retry

Retry re-executes an operation when a transient failure is expected to clear on a later attempt. Use it for bounded, idempotent calls such as inventory lookups, remote reads, or message handoffs where a temporary `503`, `429`, timeout, or similar signal should not fail the workflow immediately.

PatternKit provides `RetryPolicy<TResult>` in `PatternKit.Cloud.Retry`.

```csharp
var policy = RetryPolicy<InventoryResponse>
    .Create("inventory-availability")
    .WithMaxAttempts(3)
    .WithInitialDelay(TimeSpan.FromMilliseconds(25))
    .WithExponentialBackoff(2)
    .HandleResult(static response => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500)
    .HandleException(static exception => exception is TimeoutException)
    .Build();

var result = await policy.ExecuteAsync(
    ct => inventoryClient.GetAvailabilityAsync("SKU-42", ct),
    cancellationToken);
```

The policy returns `RetryResult<TResult>` so callers can inspect success, attempts, final value, and the last handled exception without needing ad hoc assertion or logging code.

## Production Notes

- Keep `MaxAttempts` bounded and pair retries with cancellation.
- Retry only idempotent work, or work protected by an idempotency key.
- Use result predicates for service status codes and exception predicates for transient exceptions.
- Prefer zero or injected delay providers in tests; production callers can use real delays and exponential backoff.

The inventory retry example shows both fluent and source-generated policy creation, plus `IServiceCollection` registration for importing applications.
