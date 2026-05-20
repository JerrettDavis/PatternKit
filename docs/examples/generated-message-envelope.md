# Generated Message Envelope

This example shows the runtime `Message<TPayload>` envelope path beside a source-generated contract factory. Use this shape when an application boundary must always attach the same headers before a message enters routers, routing slips, sagas, mailboxes, or reliability components.

Source:

- `src/PatternKit.Examples/Messaging/MessageEnvelopeExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MessageEnvelopeExampleTests.cs`

## Runtime Path

```csharp
var message = Message<OrderAccepted>
    .Create(new OrderAccepted("order-42", 199.95m))
    .WithMessageId("msg-100")
    .WithCorrelationId("order-42")
    .WithCausationId("checkout-7")
    .WithIdempotencyKey("order-42:accepted")
    .WithContentType("application/vnd.patternkit.order+json");
```

The fluent path is useful when headers come from configuration, a transport adapter, or user-owned middleware.

## Generated Path

```csharp
[GenerateMessageEnvelope(typeof(OrderAccepted), FactoryName = "CreateAccepted")]
[MessageEnvelopeHeader("message-id", typeof(string), ParameterName = "messageId")]
[MessageEnvelopeHeader("correlation-id", typeof(string), ParameterName = "correlationId")]
[MessageEnvelopeHeader("causation-id", typeof(string), ParameterName = "causationId")]
[MessageEnvelopeHeader("idempotency-key", typeof(string), ParameterName = "idempotencyKey")]
[MessageEnvelopeHeader("content-type", typeof(string), ParameterName = "contentType")]
public static partial class GeneratedOrderAcceptedEnvelope;
```

The generated factory requires every declared header as a typed parameter:

```csharp
var message = GeneratedOrderAcceptedEnvelope.CreateAccepted(
    new OrderAccepted("order-42", 199.95m),
    "msg-100",
    "order-42",
    "checkout-7",
    "order-42:accepted",
    "application/vnd.patternkit.order+json");

var context = GeneratedOrderAcceptedEnvelope.CreateContext(message);
```

## DI Integration

The example is importable through the standard container:

```csharp
services.AddGeneratedMessageEnvelopeExample();
var runner = provider.GetRequiredService<GeneratedMessageEnvelopeExample>().Runner;
var generated = runner.RunGenerated();
```

The extension registers the runner and production-readiness descriptor used by the examples catalog.
