# Dynamic Router

`DynamicRouter<TPayload, TResult>` keeps an ordered route table that can be changed at runtime without rebuilding the application host. Use it when operations, tenant onboarding, regional failover, or partner integrations need to add, replace, or remove message routes while the process is running.

The fluent API builds the initial table:

```csharp
var router = DynamicRouter<FulfillmentOrder, FulfillmentRouteDecision>.Create()
    .When("vip", 1, static (message, _) => message.Payload.Total >= 1_000m)
    .Then(static (_, _) => new FulfillmentRouteDecision("vip", "white-glove"))
    .Default(static (_, _) => new FulfillmentRouteDecision("default", "standard"))
    .Build();
```

The runtime API can replace or remove routes:

```csharp
router.Register(
    "region:west",
    5,
    static (message, _) => message.Payload.Region == "west",
    static (_, _) => new FulfillmentRouteDecision("region:west", "west-overflow"));

router.Unregister("region:west");
```

The source-generated path uses `[GenerateDynamicRouter]` and `[DynamicRoute]` to emit the same typed factory shape while leaving the returned router mutable for runtime route-table changes.

Production examples:

- `OrderDynamicRouterExample` demonstrates fluent and generated fulfillment routing.
- `AddOrderDynamicRouterDemo()` imports the generated router, routing service, and example runner into `IServiceCollection`.
- TinyBDD tests validate fluent behavior, generated parity, runtime replacement, aggregate `AddPatternKitExamples()` registration, and catalog coverage.
