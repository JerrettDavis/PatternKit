# Order Event Notification

The order event notification example publishes compact `OrderAcceptedNotificationEvent` notifications through an importable service.

```csharp
services.AddOrderEventNotificationDemo();

var runner = provider.GetRequiredService<OrderEventNotificationDemoRunner>();
var summary = runner.RunGenerated(new OrderAcceptedNotificationEvent("O-100", "C-900", "web", true));
```

The example includes fluent and source-generated construction plus an `IServiceCollection` extension for standard .NET hosts.
