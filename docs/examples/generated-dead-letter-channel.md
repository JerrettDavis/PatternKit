# Generated Dead Letter Channel

The generated dead-letter channel example models a checkout fulfillment boundary where failed warehouse or carrier messages are captured with reason, attempt count, original headers, and replay metadata.

Production applications can import the example through `IServiceCollection`:

```csharp
var services = new ServiceCollection();
services.AddFulfillmentDeadLetterChannelExample();

using var provider = services.BuildServiceProvider();
var workflow = provider.GetRequiredService<FulfillmentDeadLetterWorkflow>();

var summary = workflow.Capture(
    FulfillmentDeadLetterChannelExample.CreateCommand("order-100"),
    "carrier timeout");
```

The example includes:

- a fluent `DeadLetterChannel<FulfillmentCommand>` path
- a source-generated `[GenerateDeadLetterChannel]` path
- an importable `FulfillmentDeadLetterWorkflow`
- TinyBDD tests for fluent, generated, and DI usage

Files:

- `src/PatternKit.Examples/Messaging/FulfillmentDeadLetterChannelExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/FulfillmentDeadLetterChannelExampleTests.cs`
