# Bounded Context

Bounded Context makes a domain boundary explicit: the capabilities it owns, the services that implement those capabilities, and the adapters required when models cross into or out of that boundary.

Use PatternKit's fluent path when the boundary is assembled dynamically or by configuration:

```csharp
var descriptor = BoundedContextDescriptor.Create("Fulfillment")
    .AddCapability("quote shipment", typeof(IShipmentQuoter))
    .AddCapability("allocate inventory", typeof(IInventoryAllocator))
    .AddAdapter("Catalog", "Fulfillment", typeof(CatalogProduct), typeof(FulfillmentItem))
    .Build();
```

Use the generated path when the boundary is stable and should be reviewed at compile time:

```csharp
[GenerateBoundedContextDescriptor("Fulfillment")]
[BoundedContextCapability("quote shipment", typeof(IShipmentQuoter))]
[BoundedContextCapability("allocate inventory", typeof(IInventoryAllocator))]
[BoundedContextAdapter("Catalog", "Fulfillment", typeof(CatalogProduct), typeof(FulfillmentItem))]
public static partial class FulfillmentContext;
```

The generated descriptor is suitable for `IServiceCollection` registration and for production-readiness checks that need to verify every context publishes its owned capabilities and model translations.
