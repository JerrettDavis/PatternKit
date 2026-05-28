# Fulfillment Bounded Context Pattern

The fulfillment example demonstrates a production-shaped context boundary around shipment quoting and inventory allocation.

The example exposes:

- a fluent `BoundedContextDescriptor`
- a generated descriptor from `GeneratedFulfillmentContext`
- `IShipmentQuoter`, `IInventoryAllocator`, and `FulfillmentPlanner`
- `AddFulfillmentBoundedContextDemo()` for `IServiceCollection`

```csharp
var services = new ServiceCollection()
    .AddFulfillmentBoundedContextDemo();

using var provider = services.BuildServiceProvider();
var planner = provider.GetRequiredService<FulfillmentPlanner>();
var plan = planner.Plan(new CatalogProduct("SKU-1", 42m));
```

The descriptor makes it clear that catalog products are translated into fulfillment items before the fulfillment model owns shipment and reservation decisions.
