# Order Telemetry Sidecar

The order telemetry sidecar example adds trace context and telemetry capture around an order submission handler.

```csharp
services.AddOrderTelemetrySidecarDemo();

var runner = provider.GetRequiredService<OrderTelemetrySidecarDemoRunner>();
var result = runner.RunGenerated(new OrderTelemetryRequest("O-100", 42m));
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, and an ASP.NET Core minimal API mapping through `MapOrderTelemetrySidecar()`.
