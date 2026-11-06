# Partner Order Messaging Bridge

This example models a partner order stream feeding an internal commerce bus.

- Partner messages arrive on `MessageChannel<PartnerBridgeOrder>`.
- `MessagingBridge<PartnerBridgeOrder, CommerceBridgeOrderEvent>` maps partner payloads into commerce events.
- Existing message headers are preserved, including correlation identifiers.
- The bridge selects `accepted` or `paid` target topics and publishes into `MessageBus<CommerceBridgeOrderEvent>`.
- `AddPartnerOrderMessagingBridgeDemo()` imports the generated bridge path through `IServiceCollection`.

The example is implemented in `src/PatternKit.Examples/Messaging/PartnerOrderMessagingBridgeExample.cs` and validated by TinyBDD tests in `test/PatternKit.Examples.Tests/Messaging/PartnerOrderMessagingBridgeExampleTests.cs`.
