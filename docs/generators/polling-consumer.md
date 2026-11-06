# Polling Consumer Generator

`[GeneratePollingConsumer]` creates a typed `PollingConsumer<TPayload>` factory.

```csharp
[GeneratePollingConsumer(typeof(ReplenishmentRequest), FactoryName = "Create", ConsumerName = "warehouse-replenishment-poller")]
public static partial class WarehousePoller
{
    [PollingConsumerSource]
    private static Message<ReplenishmentRequest>? Poll(MessageContext context) => TryReadNext();
}
```

The source method must be static, return `Message<TPayload>?`, and accept a `MessageContext`.

Diagnostics:

- `PKPOLL001`: host type must be partial.
- `PKPOLL002`: exactly one polling source is required.
- `PKPOLL003`: polling source signature is invalid.
