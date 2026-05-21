# Fulfillment Circuit Breaker

This example models a production fulfillment gateway that can return repeated transient failures. It demonstrates the same circuit breaker rule through:

- a fluent `CircuitBreakerPolicy<FulfillmentResponse>`
- a source-generated circuit breaker policy factory
- an `IServiceCollection` extension that imports the demo into a standard .NET host

```csharp
var services = new ServiceCollection();
services.AddFulfillmentCircuitBreakerDemo();

using var provider = services.BuildServiceProvider();
var fulfillment = provider.GetRequiredService<FulfillmentCircuitBreakerService>();

var first = await fulfillment.SubmitAsync("ORDER-42");
var second = await fulfillment.SubmitAsync("ORDER-42");
var rejected = await fulfillment.SubmitAsync("ORDER-42");
```

The registered demo uses the generated policy path and a scripted fulfillment gateway. Applications can replace `IFulfillmentGateway` with their own implementation while keeping the same policy registration shape.

The accompanying TinyBDD tests validate the fluent path, the generated path, and the DI integration.
