# Order Event-Driven Consumer

The order event-driven consumer example handles pushed order-accepted events and writes an audit entry.

```csharp
services.AddOrderEventDrivenConsumerDemo();

var service = provider.GetRequiredService<OrderEventDrivenConsumerService>();
var summary = service.Accept(new OrderAcceptedEvent("ORDER-100", 42.50m));
```

The example includes fluent and source-generated construction, a push-based service boundary, and `IServiceCollection` registration for existing .NET applications.
