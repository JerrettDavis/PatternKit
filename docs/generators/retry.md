# Retry Generator

The retry generator creates a strongly typed `RetryPolicy<TResult>` factory from declarative attributes. It is useful when a team wants retry rules to live beside the operation contract while still producing the same runtime policy as the fluent API.

```csharp
[GenerateRetryPolicy(
    typeof(InventoryResponse),
    FactoryMethodName = nameof(CreateGeneratedPolicy),
    PolicyName = "inventory-availability",
    MaxAttempts = 3,
    InitialDelayMilliseconds = 25,
    BackoffFactor = 2)]
public static partial class InventoryRetryPolicy
{
    [RetryResultPredicate]
    private static bool ShouldRetry(InventoryResponse response)
        => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500;

    [RetryExceptionPredicate]
    private static bool ShouldRetry(Exception exception)
        => exception is TimeoutException;
}
```

The generated factory returns `PatternKit.Cloud.Retry.RetryPolicy<InventoryResponse>` and applies any declared result and exception predicates.

## Rules

- The host type must be `partial`.
- `MaxAttempts` must be at least `1`.
- `InitialDelayMilliseconds` must be non-negative.
- `BackoffFactor` must be at least `1`.
- Result predicates must be `static bool` methods with one `TResult` parameter.
- Exception predicates must be `static bool` methods with one `Exception` parameter.

Diagnostics use the `PKRET` prefix.
