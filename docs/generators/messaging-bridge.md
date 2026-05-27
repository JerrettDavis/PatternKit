# Messaging Bridge Generator

`[GenerateMessagingBridge]` emits the typed bridge factory for a channel-to-bus integration boundary.

```csharp
[GenerateMessagingBridge(
    typeof(PartnerBridgeOrder),
    typeof(CommerceBridgeOrderEvent),
    FactoryName = "Create",
    BridgeName = "partner-order-bridge")]
public static partial class GeneratedPartnerOrderMessagingBridge;
```

The generated factory returns a `MessagingBridge<TInbound,TOutbound>.Builder`, so application code still supplies concrete channels, target bus, translation, and topic selection through fluent composition:

```csharp
var bridge = GeneratedPartnerOrderMessagingBridge.Create()
    .From(partnerOrders)
    .To(commerceBus)
    .PreserveHeaders(order => new CommerceBridgeOrderEvent(order.PartnerOrderId, order.State, order.Amount))
    .SelectTopic(message => message.Payload.State)
    .Build();
```

Diagnostics:

| ID | Meaning |
| --- | --- |
| `PKMBR001` | The host type must be `partial`. |
| `PKMBR002` | `FactoryName` and `BridgeName` must be non-empty. |
