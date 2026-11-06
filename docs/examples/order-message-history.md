# Order Message History

This example models an order moving through a checkout API and fulfillment router.

- `MessageHistory<HistoryOrder>` appends handling steps to the message envelope.
- The fluent path builds the same recorders by hand.
- The generated path uses `[GenerateMessageHistory]` for stable component factories.
- Correlation metadata remains on the message while history entries accumulate.
- `AddOrderMessageHistoryDemo()` imports the generated path through `IServiceCollection`.

The example is implemented in `src/PatternKit.Examples/Messaging/OrderMessageHistoryExample.cs` and validated by TinyBDD tests in `test/PatternKit.Examples.Tests/Messaging/OrderMessageHistoryExampleTests.cs`.
