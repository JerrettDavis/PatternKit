# Bulkhead Generator

The bulkhead generator creates a strongly typed `BulkheadPolicy<TResult>` factory from declarative attributes. It is useful when capacity limits should be configured beside the operation contract while still producing the same runtime policy as the fluent API.

```csharp
[GenerateBulkheadPolicy(
    typeof(ShippingAllocation),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "shipping-allocation",
    MaxConcurrency = 4,
    MaxQueueLength = 16,
    QueueTimeoutMilliseconds = 250)]
public static partial class ShippingBulkheadPolicy;
```

The generated factory returns `PatternKit.Cloud.Bulkhead.BulkheadPolicy<ShippingAllocation>`.

## Rules

- The host type must be `partial`.
- `MaxConcurrency` must be at least `1`.
- `MaxQueueLength` must be non-negative.
- `QueueTimeoutMilliseconds` must be non-negative.

Diagnostics use the `PKBH` prefix.
