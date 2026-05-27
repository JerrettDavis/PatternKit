# Correlation Identifier

Correlation Identifier keeps every message in a logical workflow tied to the same business operation. Use it when a request produces replies, follow-up commands, audit events, or downstream work that must be traced as one flow.

The fluent API lives in `PatternKit.Messaging.Correlation`:

```csharp
var correlation = CorrelationIdentifier<Order>.Create()
    .Select((message, _) => "order:" + message.Payload.Id)
    .Build();

var request = correlation.Ensure(Message<Order>.Create(order).WithMessageId("msg-100"));
var reply = correlation.CorrelateReply(Message<OrderAccepted>.Create(accepted), request);
```

`Ensure` preserves an existing `correlation-id` by default, then tries the configured selector, then falls back to a generated identifier. `CorrelateReply` copies the request correlation id, or its message id when no correlation id exists.

Use a custom header when integrating with an existing transport:

```csharp
var correlation = CorrelationIdentifier<Order>.Create()
    .Header("X-Correlation")
    .Select((message, _) => message.Payload.CustomerId)
    .Build();
```
