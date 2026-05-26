# Message Bus Generator

`[GenerateMessageBus]` emits a `MessageBus<TPayload>` factory from static channel factory methods marked with `[MessageBusRoute]`.

```csharp
[GenerateMessageBus(typeof(BusOrderEvent), FactoryName = "Create", BusName = "order-bus")]
public static partial class GeneratedOrderMessageBus
{
    [MessageBusRoute("accepted")]
    private static MessageChannel<BusOrderEvent> Fulfillment()
        => MessageChannel<BusOrderEvent>.Create("fulfillment-orders").Build();
}
```

The generated factory returns a normal `MessageBus<TPayload>`, so applications can still call `Subscribe` for runtime bolt-ons after the generated topology is created.

Diagnostics:

| ID | Meaning |
| --- | --- |
| `PKBUS001` | The host type must be partial. |
| `PKBUS002` | At least one `[MessageBusRoute]` method is required. |
| `PKBUS003` | Route methods must be static, parameterless, and return `MessageChannel<TPayload>`. |
