# Mailbox

The Mailbox pattern gives one in-process consumer exclusive access to a stream of messages. PatternKit's mailbox is a lightweight serialized inbox for background workers, queue consumers, and application services that need ordered, non-concurrent handling without adopting an actor framework.

Use a mailbox when work can stay in memory and must not run concurrently inside one process. Use an external queue or actor runtime when messages must survive process failure, cross process boundaries, rebalance across nodes, or participate in broker delivery guarantees.

## Runtime API

```csharp
using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;

using var mailbox = Mailbox<OrderWork>.Create(async (message, context, cancellationToken) =>
    {
        await HandleOrderAsync(message.Payload, cancellationToken);
    })
    .Bounded(capacity: 128, MailboxBackpressurePolicy.Wait)
    .OnError(MailboxErrorPolicy.Continue)
    .Build();

await mailbox.StartAsync();
await mailbox.PostAsync(Message<OrderWork>.Create(work));
await mailbox.StopAsync();
```

`Mailbox<TPayload>` processes accepted messages through a single-consumer pump. Handlers never run concurrently for the same mailbox, and `StopAsync()` drains queued messages by default.

## Capacity and Backpressure

Mailboxes are unbounded unless configured with `Bounded`:

```csharp
var mailbox = Mailbox<OrderWork>.Create(handler)
    .Bounded(32, MailboxBackpressurePolicy.Reject)
    .Build();
```

Backpressure policies are explicit:

- `Wait` waits for queue space or enqueue cancellation.
- `Reject` returns `MailboxPostStatus.Rejected` when the queue is full.
- `DropNewest` drops the incoming message.
- `DropOldest` drops the oldest queued message and accepts the incoming message.

`MailboxPostResult` reports whether a post was accepted, rejected, or dropped. Accepted messages receive a monotonic mailbox sequence number.

## Error Handling

By default, a handler failure stops the mailbox and drops queued messages. Configure `Continue` and an error handler when later messages should still run:

```csharp
var mailbox = Mailbox<OrderWork>.Create(handler)
    .OnError(MailboxErrorPolicy.Continue, async (exception, message, context, cancellationToken) =>
    {
        await WriteDeadLetterAsync(message, exception, cancellationToken);
    })
    .Build();
```

Error callbacks are in-process hooks. They are useful for logs, dead-letter adapters, and counters, but they are not durable storage.

## Lifecycle and Shutdown

Call `StartAsync()` before posting. Call `StopAsync()` during shutdown:

- `StopAsync()` stops accepting new posts and drains queued messages.
- `StopAsync(drain: false)` drops queued messages and cancels the current handler through the handler cancellation token.

Message contexts are preserved. If the supplied `MessageContext` has a cancellation token, the mailbox links it with the mailbox stop token before invoking the handler.

## Metrics Hooks

`OnEvent` exposes lightweight lifecycle, enqueue, processing, drop, and failure events without taking a dependency on a metrics library:

```csharp
var mailbox = Mailbox<OrderWork>.Create(handler)
    .OnEvent(evt => counters.Record(evt.Kind, evt.QueuedCount))
    .Build();
```

## Choosing Related Patterns

- Use `Mailbox<TPayload>` when one in-process consumer must serialize work.
- Use [Observer](../behavioral/observer/index.md) when subscribers react to notifications and independent handlers may run separately.
- Use [Mediator](../behavioral/mediator/index.md) or [Source-Generated Dispatcher](dispatcher.md) when the goal is request/notification routing, not serialized queueing.
- Use [Enterprise Message Routing](message-routing.md) when messages need deterministic route selection, fan-out, splitting, or aggregation.
- Use external queues when durability, retries across process restarts, visibility timeouts, or cross-service delivery matter.

## API

- <xref:PatternKit.Messaging.Mailboxes.Mailbox`1>
- <xref:PatternKit.Messaging.Mailboxes.MailboxBackpressurePolicy>
- <xref:PatternKit.Messaging.Mailboxes.MailboxErrorPolicy>
- <xref:PatternKit.Messaging.Mailboxes.MailboxPostResult>
- <xref:PatternKit.Messaging.Mailboxes.MailboxPostStatus>
- <xref:PatternKit.Messaging.Mailboxes.MailboxEvent>
- <xref:PatternKit.Messaging.Mailboxes.MailboxEventKind>

## Example Source

- `src/PatternKit.Examples/Messaging/MailboxExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MailboxExampleTests.cs`
