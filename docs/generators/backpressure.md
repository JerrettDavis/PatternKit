# Backpressure Generator

`[GenerateBackpressurePolicy]` emits a static factory for `BackpressurePolicy<TResult>` with compile-time configuration for name, capacity, saturation mode, and wait timeout.

```csharp
[GenerateBackpressurePolicy(
    typeof(CheckoutAdmission),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "checkout-backpressure",
    Capacity = 8,
    Mode = "Wait",
    WaitTimeoutMilliseconds = 50)]
public static partial class GeneratedCheckoutBackpressurePolicy;
```

Diagnostics:

- `PKBP001`: the host type must be partial.
- `PKBP002`: capacity and wait timeout must be valid.
- `PKBP003`: the factory method name must be a valid identifier.
- `PKBP004`: mode must be one of `Reject`, `Wait`, `DropNewest`, `DropOldest`, `Shed`, or `Observe`.
