# Strangler Fig

Strangler Fig routes traffic between a legacy implementation and a modern replacement while migration rules are rolled out incrementally.

```csharp
var migration = StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse>
    .Create("checkout-strangler")
    .RouteToModern("enterprise-tenant", request => request.TenantId.StartsWith("enterprise-"))
    .RouteToModern("large-order-pilot", request => request.Total >= 1_000m)
    .Legacy(legacy.Submit)
    .Modern(modern.Submit)
    .Build();

var result = migration.Route(request);
```

Use it at an API facade, gateway, service layer, or endpoint boundary when an existing system is being replaced feature-by-feature. The runtime result records whether the request used the legacy or modern path and which migration rule matched.

The source-generated path uses `[GenerateStranglerFig]`, `[StranglerFigRoute]`, `[StranglerFigLegacy]`, and `[StranglerFigModern]`. Import the example through `AddCheckoutStranglerFigDemo()` or `AddPatternKitExamples()`.
