# Order Wire Tap

The order wire-tap example records audit and metrics side effects while forwarding the original order event unchanged. It demonstrates:

- a fluent `WireTap<OrderWireTapEvent>` for non-generator consumers
- a `[GenerateWireTap]` source-generated factory
- `IServiceCollection` registration through `AddOrderWireTapDemo()`
- aggregate import through `AddPatternKitExamples()`

The example is implemented in `src/PatternKit.Examples/Messaging/OrderWireTapExample.cs` and covered by TinyBDD tests in `test/PatternKit.Examples.Tests/Messaging/OrderWireTapExampleTests.cs`.
