# Bounded Context Generator

`BoundedContextDescriptorGenerator` emits a `BoundedContextDescriptor` factory from attributes on a partial class or struct.

```csharp
[GenerateBoundedContextDescriptor("Fulfillment", FactoryMethodName = "Build")]
[BoundedContextCapability("quote shipment", typeof(IShipmentQuoter))]
[BoundedContextAdapter("Catalog", "Fulfillment", typeof(CatalogProduct), typeof(FulfillmentItem))]
public static partial class FulfillmentContext;
```

The generated method creates the descriptor, adds capabilities in deterministic order, adds model adapters, and returns the built descriptor.

Diagnostics:

| Id | Meaning |
| --- | --- |
| `PKCTX001` | The host type must be partial. |
| `PKCTX002` | At least one capability is required. |
| `PKCTX003` | Capability names must be unique within the context. |
| `PKCTX004` | Adapter registrations must be unique. |
