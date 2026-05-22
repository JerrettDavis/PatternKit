# Messaging Gateway

Messaging Gateway exposes an application-friendly request/response method while hiding message channel and envelope plumbing.

```csharp
var gateway = MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision>
    .Create("payment-authorization-gateway")
    .SendTo(requestChannel)
    .Handle((message, context) => Message<PaymentAuthorizationDecision>.Create(decision))
    .Build();

var result = gateway.Invoke(new PaymentAuthorizationRequest("ORDER-100", 42.50m));
```

Use it when application services should call a typed API while the implementation still sends a message through a channel and handles a message-shaped response. The gateway reports channel rejection explicitly so callers can distinguish backpressure from business decisions.

The source-generated path uses `[GenerateMessagingGateway]` and `[MessagingGatewayHandler]`. Import the payment example through `AddPaymentMessagingGatewayDemo()` or `AddPatternKitExamples()`.
