# Dead Letter Channel Generator

`[GenerateDeadLetterChannel]` emits a factory for `DeadLetterChannel<TPayload>` from a partial host and a store factory method.

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Reliability;

[GenerateDeadLetterChannel(
    typeof(FulfillmentCommand),
    FactoryName = "CreateChannel",
    ChannelName = "fulfillment-dead-letter",
    Source = "checkout.fulfillment",
    IdPrefix = "fulfillment-dead")]
public static partial class FulfillmentDeadLetters
{
    [DeadLetterStoreFactory]
    private static IDeadLetterStore<FulfillmentCommand> CreateStore()
        => new InMemoryDeadLetterStore<FulfillmentCommand>();
}
```

The generated factory:

- creates a named dead-letter channel
- records the configured source in captured metadata
- uses the marked store factory for application-owned persistence
- generates deterministic ids from the configured prefix and message id
- enables exception detail capture by default

## Diagnostics

| ID | Meaning |
| --- | --- |
| `PKDL001` | The host type marked with `[GenerateDeadLetterChannel]` must be partial. |
| `PKDL002` | The host must declare exactly one `[DeadLetterStoreFactory]` method. |
| `PKDL003` | The store factory must be static, parameterless, and return `IDeadLetterStore<TPayload>`. |

## Example

See [Generated Dead Letter Channel](../examples/generated-dead-letter-channel.md).
