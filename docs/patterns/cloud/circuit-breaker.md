# Circuit Breaker

Circuit Breaker protects callers from repeatedly invoking an unhealthy dependency. It opens after a configured number of handled failures, rejects calls while the break window is active, and allows a half-open probe when the dependency is eligible to recover.

PatternKit provides `CircuitBreakerPolicy<TResult>` in `PatternKit.Cloud.CircuitBreaker`.

```csharp
var policy = CircuitBreakerPolicy<FulfillmentResponse>
    .Create("fulfillment-gateway")
    .WithFailureThreshold(2)
    .WithBreakDuration(TimeSpan.FromSeconds(30))
    .HandleResult(static response => response.StatusCode == 408 || response.StatusCode == 429 || response.StatusCode >= 500)
    .HandleException(static exception => exception is TimeoutException)
    .Build();

var result = await policy.ExecuteAsync(
    ct => fulfillmentGateway.SubmitAsync("ORDER-42", ct),
    cancellationToken);
```

The policy returns `CircuitBreakerResult<TResult>` so callers can inspect success, state, failure count, rejection, value, and handled exception details without manual assertion plumbing.

## Production Notes

- Use circuit breakers around remote calls, infrastructure clients, and other dependency boundaries.
- Keep the failure threshold and break duration aligned with the dependency's real recovery profile.
- Treat rejected results as a distinct operational signal; callers can fallback, enqueue, or fail fast without touching the dependency.
- Pair with cancellation for async operations.
- Use `WithClock` in tests to validate open and half-open transitions deterministically.

The fulfillment circuit breaker example shows both fluent and source-generated policy creation, plus `IServiceCollection` registration for importing applications.
