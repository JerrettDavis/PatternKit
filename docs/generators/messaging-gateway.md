# Messaging Gateway Generator

`[GenerateMessagingGateway]` creates a typed `MessagingGateway<TRequest, TResponse>` factory.

```csharp
[GenerateMessagingGateway(typeof(PaymentAuthorizationRequest), typeof(PaymentAuthorizationDecision), FactoryName = "Create", GatewayName = "payment-authorization-gateway")]
public static partial class PaymentGateway
{
    [MessagingGatewayHandler]
    private static Message<PaymentAuthorizationDecision> Authorize(Message<PaymentAuthorizationRequest> request, MessageContext context)
        => Message<PaymentAuthorizationDecision>.Create(new("AUTH-100", true));
}
```

The generated factory accepts a `MessageChannel<TRequest>` so the gateway can be composed through `IServiceCollection`.

Diagnostics:

- `PKGWY001`: host type must be partial.
- `PKGWY002`: exactly one messaging gateway handler is required.
- `PKGWY003`: messaging gateway handler signature is invalid.
