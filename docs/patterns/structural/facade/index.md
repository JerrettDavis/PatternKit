# Facade Pattern

> **TL;DR**: Provide a simplified, unified interface to a complex subsystem. PatternKit's implementation offers string-based operations (`Facade<TIn, TOut>`) and type-safe interfaces (`TypedFacade<T>`).

## Quick Example

```csharp
using PatternKit.Structural.Facade;

// Simple facade with named operations
var orderFacade = Facade<OrderRequest, OrderResult>.Create()
    .Operation("process", (in OrderRequest req) =>
    {
        var inventory = inventoryService.Reserve(req.Items);
        var payment = paymentService.Charge(req.Payment);
        var shipment = shippingService.Schedule(req.Address);
        return new OrderResult(inventory, payment, shipment);
    })
    .Operation("cancel", (in OrderRequest req) =>
    {
        inventoryService.Release(req.OrderId);
        paymentService.Refund(req.OrderId);
        return new OrderResult { Status = "Cancelled" };
    })
    .Default((in OrderRequest req) => new OrderResult { Status = "Unknown" })
    .Build();

var result = orderFacade.Execute("process", request);
```

## What It Is

The **Facade** pattern provides a simplified interface to a complex subsystem. Instead of requiring clients to understand and coordinate multiple services, they invoke a single named operation that handles the complexity internally.

PatternKit provides two facade implementations:

1. **`Facade<TIn, TOut>`**: String-based operations with runtime resolution
2. **`TypedFacade<T>`**: Compile-time safe facade using interface contracts

Both approaches produce immutable, thread-safe facades that coordinate subsystem interactions behind simple operation names.

## When to Use

- **Multiple service coordination**: Operations require calling several services in sequence
- **Complex subsystem hiding**: Shield clients from intricate API details
- **Legacy system wrapping**: Provide modern interface to legacy code
- **API simplification**: Reduce learning curve for external consumers
- **Microservice orchestration**: Coordinate multiple service calls into single operations

## When to Avoid

- **Simple 1-to-1 mappings**: Direct calls are clearer and faster
- **Single service operations**: No coordination complexity to hide
- **Unrelated operations**: Mixing unrelated concerns dilutes facade purpose
- **Performance-critical paths**: Named operation lookup adds overhead

## Pattern Variants

| Variant | Use Case |
|---------|----------|
| `Facade<TIn, TOut>` | String-based operations, runtime flexibility |
| `TypedFacade<T>` | Compile-time safety, IntelliSense support |

## See Also

- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
- [Real-World Examples](real-world-examples.md)
