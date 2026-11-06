# Circuit Breaker Generator

The circuit breaker generator creates a strongly typed `CircuitBreakerPolicy<TResult>` factory from declarative attributes. It is useful when resilience rules should live next to the operation contract while still producing the same runtime policy as the fluent API.

```csharp
[GenerateCircuitBreakerPolicy(
    typeof(FulfillmentResponse),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "fulfillment-gateway",
    FailureThreshold = 2,
    BreakDurationMilliseconds = 30000)]
public static partial class FulfillmentCircuitBreakerPolicy
{
    [CircuitBreakerResultPredicate]
    private static bool ShouldOpen(FulfillmentResponse response)
        => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500;

    [CircuitBreakerExceptionPredicate]
    private static bool ShouldOpen(Exception exception)
        => exception is TimeoutException;
}
```

The generated factory returns `PatternKit.Cloud.CircuitBreaker.CircuitBreakerPolicy<FulfillmentResponse>` and applies any declared result and exception predicates.

## Rules

- The host type must be `partial`.
- `FailureThreshold` must be at least `1`.
- `BreakDurationMilliseconds` must be non-negative.
- Result predicates must be `static bool` methods with one `TResult` parameter.
- Exception predicates must be `static bool` methods with one `Exception` parameter.

Diagnostics use the `PKCB` prefix.
