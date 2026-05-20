# Idempotent Receiver, Inbox, and Outbox

PatternKit provides small in-process helpers for idempotency and reliable handoff boundaries. They are designed for application composition: you own the storage, broker, transaction boundary, and retry policy.

These APIs do not claim exactly-once delivery. They help make at-least-once message handling safer by checking an idempotency key before running a handler and by modeling outbox records that can be persisted and dispatched by application code.

## Idempotent Receiver

`IdempotentReceiver<TPayload, TResult>` wraps a handler with an idempotency-key claim:

```csharp
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

var store = new InMemoryIdempotencyStore();

var receiver = IdempotentReceiver<AcceptOrder, string>.Create(
        store,
        async (message, context, cancellationToken) =>
        {
            await SaveOrderAsync(message.Payload, cancellationToken);
            return message.Payload.OrderId;
        })
    .OnDuplicate(DuplicateMessagePolicy.ReplayCompleted)
    .Build();

var result = await receiver.HandleAsync(
    Message<AcceptOrder>
        .Create(command)
        .WithIdempotencyKey("accept-order-42"));
```

The default key selector reads `MessageHeaderNames.IdempotencyKey`. Use `KeyBy` when keys live in a payload or another header.

Duplicate policies are explicit:

- `Suppress` returns `Duplicate` and does not call the handler.
- `ReplayCompleted` returns a stored completed result when the result is assignable to `TResult`; otherwise it suppresses the duplicate.

Missing key policies are explicit:

- `Reject` returns `MissingKey` and does not call the handler.
- `Process` calls the handler without idempotency protection.

When the handler succeeds, the receiver calls `IIdempotencyStore.MarkCompletedAsync`. When the handler throws, it calls `MarkFailedAsync` and rethrows.

## Idempotency Store

`IIdempotencyStore` is intentionally small:

```csharp
ValueTask<IdempotencyClaim> TryClaimAsync(string key, CancellationToken cancellationToken);
ValueTask MarkCompletedAsync(string key, object? result, CancellationToken cancellationToken);
ValueTask MarkFailedAsync(string key, string? reason, CancellationToken cancellationToken);
```

`InMemoryIdempotencyStore` is useful for tests, demos, and single-process tools. Production systems should back the interface with the same durable store that protects the related side effect, usually through a unique key or compare-and-set operation.

## Inbox Processor

`InboxProcessor<TPayload, TResult>` is a small wrapper around an idempotent receiver. It gives application code a named inbox boundary without forcing a storage provider:

```csharp
var inbox = InboxProcessor<AcceptOrder, string>.Create(receiver);
var result = await inbox.ProcessAsync(message, context, cancellationToken);
```

Compose it with [Mailbox](mailbox.md) when work must also be serialized in process, or with [Enterprise Message Routing](message-routing.md) when messages need route selection before handling.

## Outbox

`OutboxMessage<TPayload>` models a pending handoff. `IOutboxDispatcher<TPayload>` sends a record to a broker, queue, HTTP client, or in-process handler. `InMemoryOutbox<TPayload>` provides a deterministic in-process implementation for tests and examples:

```csharp
var outbox = new InMemoryOutbox<OrderAccepted>();

await outbox.EnqueueAsync(
    Message<OrderAccepted>.Create(new OrderAccepted(orderId)),
    id: $"accepted-{orderId}");

await outbox.DispatchPendingAsync(dispatcher, cancellationToken);
```

The in-memory outbox records attempts and dispatch timestamps, but it is not durable. A production outbox should persist `OutboxMessage<TPayload>` or an equivalent schema in the same transaction as the business state change, then dispatch records after commit.

## Source-Generated Reliability Pipeline

`[GenerateReliabilityPipeline]` generates the static factories for a stable idempotent receiver, inbox, and outbox contract:

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateReliabilityPipeline(
    typeof(AcceptOrder),
    typeof(string),
    typeof(OrderAccepted),
    DuplicatePolicy = "ReplayCompleted")]
public static partial class OrderReliability
{
    [ReliabilityHandler]
    private static ValueTask<string> Handle(
        Message<AcceptOrder> message,
        MessageContext context,
        CancellationToken cancellationToken)
        => new(message.Payload.OrderId);
}
```

The generated host exposes receiver, inbox, and outbox factory methods while keeping the handler and optional key selector in source. This makes reliability topology visible during code review and importable through normal `IServiceCollection` registration.

## Boundaries

- These APIs help with at-least-once processing; they do not provide exactly-once delivery.
- `InMemoryIdempotencyStore` and `InMemoryOutbox<TPayload>` are not durable across process restarts.
- A durable implementation should make idempotency claims atomically, usually with a unique key.
- A durable outbox should persist records before dispatch and mark them dispatched only after the transport accepts them.
- Handler side effects must still be designed to tolerate retries around process, database, or broker failures.

## API

- <xref:PatternKit.Messaging.Reliability.IdempotentReceiver`2>
- <xref:PatternKit.Messaging.Reliability.IdempotentReceiverResult`1>
- <xref:PatternKit.Messaging.Reliability.IdempotentReceiverStatus>
- <xref:PatternKit.Messaging.Reliability.IIdempotencyStore>
- <xref:PatternKit.Messaging.Reliability.InMemoryIdempotencyStore>
- <xref:PatternKit.Messaging.Reliability.IdempotencyClaim>
- <xref:PatternKit.Messaging.Reliability.IdempotencyEntryStatus>
- <xref:PatternKit.Messaging.Reliability.DuplicateMessagePolicy>
- <xref:PatternKit.Messaging.Reliability.MissingIdempotencyKeyPolicy>
- <xref:PatternKit.Messaging.Reliability.InboxProcessor`2>
- <xref:PatternKit.Messaging.Reliability.OutboxMessage`1>
- <xref:PatternKit.Messaging.Reliability.IOutboxDispatcher`1>
- <xref:PatternKit.Messaging.Reliability.InMemoryOutbox`1>
- <xref:PatternKit.Generators.Messaging.GenerateReliabilityPipelineAttribute>

## Example Source

- `src/PatternKit.Examples/Messaging/ReliabilityExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/ReliabilityExampleTests.cs`
