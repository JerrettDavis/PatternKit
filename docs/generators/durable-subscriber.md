# Durable Subscriber Generator

`[GenerateDurableSubscriber]` emits a typed factory for `DurableSubscriber<TPayload>`.

```csharp
[GenerateDurableSubscriber(typeof(OrderShipmentEvent), SubscriberName = "shipment-projection")]
public static partial class GeneratedOrderDurableSubscriber
{
    [DurableSubscriberHandler("project")]
    private static DurableSubscriberHandlerResult Project(
        StoredMessage<OrderShipmentEvent> message,
        MessageContext context)
        => DurableSubscriberHandlerResult.Success("project");
}

var subscriber = GeneratedOrderDurableSubscriber.Create(messageStore, checkpointStore);
```

Generated factories require the store and checkpoint store as parameters so applications keep ownership of persistence and container lifetimes.

## Diagnostics

| Id | Meaning |
|---|---|
| `PKDS001` | The target type must be partial. |
| `PKDS002` | At least one `[DurableSubscriberHandler]` method is required. |
| `PKDS003` | Handler methods must be static and return `DurableSubscriberHandlerResult` with `StoredMessage<TPayload>` and `MessageContext` parameters. |
