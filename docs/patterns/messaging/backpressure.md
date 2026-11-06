# Backpressure

Backpressure protects a busy application boundary by making saturation explicit. `BackpressurePolicy<TResult>` bounds active work and applies one configured behavior when capacity is exhausted: reject, wait, drop newest, drop oldest, shed, or observe.

Use the fluent route when the application owns policy composition:

```csharp
var policy = BackpressurePolicy<CheckoutAdmission>.Create("checkout-backpressure")
    .WithCapacity(8)
    .WithMode(BackpressureMode.Wait)
    .WithWaitTimeout(TimeSpan.FromMilliseconds(50))
    .Build();
```

Use the generated route when a stable policy belongs to a host type:

```csharp
[GenerateBackpressurePolicy(typeof(CheckoutAdmission), Mode = "Wait", Capacity = 8)]
public static partial class CheckoutBackpressure;
```

The policy is also available through `AddPatternKitBackpressurePolicy<TResult>` for standard `IServiceCollection` integration.
