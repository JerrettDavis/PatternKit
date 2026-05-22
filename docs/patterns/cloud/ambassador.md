# Ambassador

Ambassador wraps outbound service calls with connectivity, transformation, telemetry, and fallback behavior.

```csharp
var ambassador = Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse>
    .Create("inventory-ambassador")
    .Transform(request => request with { Sku = request.Sku.ToUpperInvariant() })
    .ConnectionPolicy(request => request.Tenant != "blocked")
    .Telemetry("trace", ctx => ctx.Items["tenant"] = ctx.Request.Tenant)
    .Call(ctx => inventory.GetAvailability(ctx.Request))
    .Fallback(ctx => new(ctx.Request.Sku, "cached", "fallback-cache"))
    .Build();

var result = ambassador.Invoke(request);
```

Use it at outbound integration boundaries where every caller needs the same connection policy, request normalization, telemetry enrichment, and fallback handling before reaching a remote dependency. The runtime path returns an explicit result with recorded events and fallback status.

The source-generated path uses `[GenerateAmbassador]`, `[AmbassadorTransform]`, `[AmbassadorConnectionPolicy]`, `[AmbassadorTelemetry]`, `[AmbassadorCall]`, and `[AmbassadorFallback]`. Import the example through `AddInventoryAmbassadorDemo()` or `AddPatternKitExamples()`.
