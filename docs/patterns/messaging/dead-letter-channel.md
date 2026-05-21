# Dead Letter Channel

Dead Letter Channel captures messages that cannot be processed or delivered after the owning pipeline has exhausted its normal handling path. PatternKit keeps the channel application-owned: you choose the store, source name, failure reason, attempt count, and replay handoff.

Use `DeadLetterChannel<TPayload>` when a message should be preserved for operations instead of being dropped or retried forever.

```csharp
var store = new InMemoryDeadLetterStore<FulfillmentCommand>();

var channel = DeadLetterChannel<FulfillmentCommand>.Create("fulfillment-dead-letter")
    .FromSource("checkout.fulfillment")
    .UseStore(store)
    .UseIds((message, reason, context) => "fulfillment-dead:" + message.Headers.MessageId)
    .Build();

var deadLetter = await channel.CaptureAsync(
    command,
    "carrier timeout",
    exception,
    attempts: 4,
    cancellationToken: ct);
```

Captured messages preserve the original payload and headers, then add operational headers such as `dead-letter-id`, `dead-letter-channel`, `dead-letter-reason`, `dead-letter-attempts`, and `dead-letter-source`.

## Replay Handoff

Replay is explicit. `PrepareReplayAsync` loads the captured message and adds replay metadata without deleting the dead-letter record:

```csharp
var replay = await channel.PrepareReplayAsync(deadLetter.Id, ct);

if (replay.ReadyForReplay)
{
    await fulfillmentInbox.ProcessAsync(replay.Message!, cancellationToken: ct);
}
```

Use a durable `IDeadLetterStore<TPayload>` implementation for production transport boundaries. `InMemoryDeadLetterStore<TPayload>` is intended for tests, samples, and embedded in-process usage.

## Source Generator

Use `[GenerateDeadLetterChannel]` when the channel name, source, and store factory are stable at compile time. See [Dead Letter Channel Generator](../../generators/dead-letter-channel.md).

## Example

The production-shaped fulfillment example demonstrates the fluent channel, generated channel, and `IServiceCollection` integration:

- `src/PatternKit.Examples/Messaging/FulfillmentDeadLetterChannelExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/FulfillmentDeadLetterChannelExampleTests.cs`
