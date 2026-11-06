# Sidecar

Sidecar attaches companion behavior to a primary operation without mixing that behavior into the application handler.

```csharp
var sidecar = Sidecar<OrderTelemetryRequest, OrderTelemetryResponse>
    .Create("order-telemetry-sidecar")
    .Before("trace-context", ctx => ctx.Items["trace-id"] = $"trace-{ctx.Request.OrderId}")
    .After("telemetry", (ctx, response) => telemetry.Capture("order.accepted", response.Confirmation))
    .Handle(ctx => new($"ACCEPTED-{ctx.Request.OrderId}", (string)ctx.Items["trace-id"]))
    .Build();

var result = sidecar.Invoke(request);
```

Use it when request tracing, metrics, connectivity policy, local proxy behavior, or other companion responsibilities should be modeled beside the primary app capability. The runtime result records completed companion steps and reports primary or companion failures explicitly.

The source-generated path uses `[GenerateSidecar]`, `[SidecarBefore]`, `[SidecarAfter]`, and `[SidecarHandler]`. Import the example through `AddOrderTelemetrySidecarDemo()` or `AddPatternKitExamples()`.
