# Checkout Backpressure

The checkout backpressure example shows a production-style admission gate in front of checkout work. It includes a fluent policy, a source-generated policy factory, and an `IServiceCollection` extension that registers the policy and workflow service.

Import it into a host:

```csharp
services.AddCheckoutBackpressureDemo();
```

For reusable app-level registration without importing examples:

```csharp
services.AddPatternKitBackpressurePolicy<CheckoutAdmission>(
    "checkout-backpressure",
    builder => builder
        .WithCapacity(8)
        .WithMode(BackpressureMode.Wait)
        .WithWaitTimeout(TimeSpan.FromMilliseconds(50)));
```
