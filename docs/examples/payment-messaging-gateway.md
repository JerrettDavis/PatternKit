# Payment Messaging Gateway

The payment messaging gateway example exposes a typed authorization method while sending requests through a PatternKit message channel.

```csharp
services.AddPaymentMessagingGatewayDemo();

var service = provider.GetRequiredService<PaymentMessagingGatewayService>();
var summary = service.Authorize(new PaymentAuthorizationRequest("ORDER-100", 42.50m));
```

The example includes fluent and source-generated construction, a message-backed request boundary, and `IServiceCollection` registration for existing .NET applications.
