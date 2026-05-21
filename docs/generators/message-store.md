# Message Store Generator

`[GenerateMessageStore]` creates a typed `MessageStore<TPayload>` factory for a partial class or struct.

```csharp
[GenerateMessageStore(typeof(OrderSubmitted), FactoryName = "Create", StoreName = "order-audit")]
public static partial class OrderAuditStore
{
    [MessageStoreIdentity]
    private static string Identity(Message<OrderSubmitted> message, MessageContext context)
        => message.Headers.MessageId!;

    [MessageStoreRetention]
    private static bool Retain(StoredMessage<OrderSubmitted> stored)
        => !stored.Message.Payload.ContainsSensitiveData;
}
```

The generated factory composes the fluent runtime API:

- `MessageStore<TPayload>.Create(StoreName)`
- optional `.IdentifyBy(...)`
- optional `.RetainWhen(...)`
- `.Build()`

Diagnostics:

- `PKMS001`: host type must be partial.
- `PKMS002`: identity method must be `static string Method(Message<TPayload>, MessageContext)`.
- `PKMS003`: retention method must be `static bool Method(StoredMessage<TPayload>)`.
- `PKMS004`: only one identity hook and one retention hook are allowed.
