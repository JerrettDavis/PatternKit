# Messaging Bridge

Messaging Bridge connects two independent message topologies through an explicit translation boundary. Use it when an external channel, partner bus, or legacy integration stream must feed an internal `MessageBus<T>` without leaking transport-specific payloads into the target model.

PatternKit's bridge reads from a `MessageChannel<TInbound>`, translates the full message envelope to `Message<TOutbound>`, selects the target topic, and publishes into the target bus. The `PreserveHeaders` helper keeps message metadata such as correlation identifiers while mapping only the payload.

```csharp
var bridge = MessagingBridge<PartnerBridgeOrder, CommerceBridgeOrderEvent>
    .Create("partner-order-bridge")
    .From(partnerOrders)
    .To(commerceBus)
    .PreserveHeaders(order => new CommerceBridgeOrderEvent(order.PartnerOrderId, order.State, order.Amount))
    .SelectTopic(message => message.Payload.State == "paid" ? "paid" : "accepted")
    .Build();

var results = bridge.BridgeAll();
```

Prefer this pattern when the important design decision is the boundary between topologies. If the payload type stays the same and only channel endpoints change, use Channel Adapter. If the main concern is payload normalization, use Message Translator or Canonical Data Model.
