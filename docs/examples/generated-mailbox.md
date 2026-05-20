# Generated Mailbox

This example shows fluent and source-generated mailbox factories side by side. A PatternKit mailbox is a serialized in-process inbox: accepted messages are handled by one consumer pump, making stateful background work deterministic without adopting an actor framework.

Use the generated path when the payload type, handler, capacity, backpressure policy, and error policy are stable application structure. The generator emits a factory returning the same `Mailbox<TPayload>` runtime type as the fluent API.

## Source

- `src/PatternKit.Examples/Messaging/MailboxExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MailboxExampleTests.cs`

## Fluent Path

```csharp
using var mailbox = Mailbox<MailboxWorkItem>.Create((message, context, cancellationToken) =>
    {
        processed.Add($"{context.Headers.GetString(MessageHeaderNames.CorrelationId)}:{message.Payload.Id}");
        return default;
    })
    .Bounded(8, MailboxBackpressurePolicy.Wait)
    .OnError(MailboxErrorPolicy.Continue)
    .Build();
```

## Source-Generated Path

```csharp
[GenerateMailbox(typeof(MailboxWorkItem), FactoryName = "CreateWorkQueue", Capacity = 8, BackpressurePolicy = "Wait", ErrorPolicy = "Continue")]
public static partial class GeneratedMailboxWorkQueue
{
    [MailboxHandler]
    private static ValueTask Handle(Message<MailboxWorkItem> message, MessageContext context, CancellationToken cancellationToken)
    {
        Processed.Add($"{context.Headers.GetString(MessageHeaderNames.CorrelationId)}:{message.Payload.Id}");
        return default;
    }
}
```

Optional `[MailboxErrorHandler]` and `[MailboxEventSink]` methods can be added when the generated factory should wire failure handling or metrics events.

## Dependency Injection

```csharp
var services = new ServiceCollection();
services.AddGeneratedMailboxExample();

using var provider = services.BuildServiceProvider(validateScopes: true);
var example = provider.GetRequiredService<GeneratedMailboxExample>();
var processed = await example.Runner.RunGeneratedAsync();
```

In a production host, register the generated mailbox itself as a singleton or wrap it behind a typed service that owns startup and shutdown. PatternKit keeps processing serialized in process; durable queues, persistence, and restart recovery remain application infrastructure.
