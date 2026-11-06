# Order Dynamic Router

The order dynamic router example shows fulfillment routing with an initial source-generated route table and runtime route replacement for regional failover.

The example includes:

- a fluent `OrderDynamicRouters.Create()` path;
- a `[GenerateDynamicRouter]` factory path;
- `FulfillmentRoutingService.ReplaceRegionalRoute(...)` for runtime route-table changes;
- `AddOrderDynamicRouterDemo()` for standard `IServiceCollection` integration;
- TinyBDD coverage for fluent, generated, runtime replacement, and aggregate `AddPatternKitExamples()` registration.

```csharp
services.AddOrderDynamicRouterDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var runner = provider.GetRequiredService<OrderDynamicRouterExampleRunner>();

var summary = runner.RunGenerated([
    new DynamicFulfillmentOrder("order-1", "central", 1_250m),
    new DynamicFulfillmentOrder("order-2", "west", 100m),
    new DynamicFulfillmentOrder("order-3", "central", 50m)
]);
```

Use this shape when routing rules need to be owned by configuration, operations, tenant onboarding, or failover workflows instead of being fixed at process startup.
