# Sidecar Generator

`[GenerateSidecar]` creates a typed `Sidecar<TRequest, TResponse>` factory from before steps, after steps, and one primary handler.

```csharp
[GenerateSidecar(typeof(OrderTelemetryRequest), typeof(OrderTelemetryResponse), SidecarName = "order-telemetry-sidecar")]
public static partial class OrderSidecars
{
    [SidecarBefore("trace-context")]
    private static void AddTrace(SidecarContext<OrderTelemetryRequest> ctx) => ctx.Items["trace-id"] = "trace-1";

    [SidecarAfter("telemetry")]
    private static void Capture(SidecarContext<OrderTelemetryRequest> ctx, OrderTelemetryResponse response) { }

    [SidecarHandler]
    private static OrderTelemetryResponse Submit(SidecarContext<OrderTelemetryRequest> ctx) => new("accepted", "trace-1");
}
```

Diagnostics:

- `PKSC001`: host type must be partial.
- `PKSC002`: at least one companion step and exactly one handler are required.
- `PKSC003`: before, after, or handler signature is invalid.
- `PKSC004`: companion step names must be unique.
