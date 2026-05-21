# Order Message Filter

The order message-filter example screens fulfillment commands before downstream processing. It demonstrates:

- a fluent `MessageFilter<OrderMessageFilterCommand>` for non-generator consumers
- a `[GenerateMessageFilter]` source-generated factory
- `IServiceCollection` registration through `AddOrderMessageFilterDemo()`
- aggregate import through `AddPatternKitExamples()`

The example is implemented in `src/PatternKit.Examples/Messaging/OrderMessageFilterExample.cs` and covered by TinyBDD tests in `test/PatternKit.Examples.Tests/Messaging/OrderMessageFilterExampleTests.cs`.
