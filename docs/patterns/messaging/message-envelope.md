# Message Envelope and Context

`Message<TPayload>` is a small immutable envelope for in-process messaging patterns. It carries a payload plus `MessageHeaders`, so routing, sagas, mailboxes, and idempotent receivers can share correlation and delivery metadata without taking a dependency on a broker.

Use it when you need consistent metadata across composed PatternKit patterns:

- correlation and causation IDs
- message IDs and idempotency keys
- content type and reply address
- execution-scoped context items for diagnostics or routing decisions

Use a durable queue, broker, or database when you need cross-process delivery, persistence, retries after process restart, or transactional outbox guarantees.

## Basic Usage

```csharp
using PatternKit.Messaging;

var message = Message<string>
    .Create("order-created")
    .WithMessageId("msg-100")
    .WithCorrelationId("order-42")
    .WithCausationId("checkout-7")
    .WithIdempotencyKey("order-42:created")
    .WithContentType("text/plain");

Console.WriteLine(message.Headers.CorrelationId); // order-42
```

Every enrichment call returns a new message or header collection. The original remains unchanged.

```csharp
var original = Message<string>.Create("payload");
var enriched = original.WithCorrelationId("corr-1");

Console.WriteLine(original.Headers.CorrelationId is null); // True
Console.WriteLine(enriched.Headers.CorrelationId);         // corr-1
```

## Headers

`MessageHeaders` is an immutable, case-insensitive header collection. Well-known names live in `MessageHeaderNames`.

```csharp
var headers = MessageHeaders.Empty
    .With(MessageHeaderNames.CorrelationId, "corr-1")
    .With("tenant", "north");

if (headers.TryGet<string>("tenant", out var tenant))
{
    Console.WriteLine(tenant);
}
```

Well-known helpers validate required string values:

```csharp
var headers = MessageHeaders.Empty
    .WithMessageId("msg-1")
    .WithCorrelationId("corr-1")
    .WithIdempotencyKey("idem-1")
    .WithReplyTo("queue:reply");
```

Typed readers cover common integration metadata:

```csharp
var id = Guid.NewGuid();
var timestamp = DateTimeOffset.UtcNow;

var headers = MessageHeaders.Empty
    .With("operation-id", id.ToString("D"))
    .WithTimestamp(timestamp);

headers.TryGetGuid("operation-id", out var operationId);
var acceptedAt = headers.Timestamp;
```

## Context

`MessageContext` carries headers, cancellation, and execution-scoped items through a routing operation. Items are intentionally separate from headers: headers describe the message, while items describe the current in-process execution.

```csharp
using var cts = new CancellationTokenSource();

var context = MessageContext
    .From(message, cts.Token)
    .WithItem("attempt", 1)
    .WithHeader("route", "billing");

if (context.TryGetItem<int>("attempt", out var attempt))
{
    Console.WriteLine(attempt);
}
```

## Source-Generated Contracts

Use `[GenerateMessageEnvelope]` when an application boundary has a stable envelope contract and every message must include the same required headers:

```csharp
using PatternKit.Generators.Messaging;

[GenerateMessageEnvelope(typeof(OrderAccepted), FactoryName = "CreateAccepted")]
[MessageEnvelopeHeader("message-id", typeof(string), ParameterName = "messageId")]
[MessageEnvelopeHeader("correlation-id", typeof(string), ParameterName = "correlationId")]
[MessageEnvelopeHeader("idempotency-key", typeof(string), ParameterName = "idempotencyKey")]
public static partial class OrderAcceptedEnvelope;
```

The generated factory returns `Message<OrderAccepted>` and requires each header as a typed parameter. The generated context factory starts an execution context from the same contract:

```csharp
var message = OrderAcceptedEnvelope.CreateAccepted(
    new OrderAccepted("order-42", 199.95m),
    "msg-100",
    "order-42",
    "order-42:accepted");

var context = OrderAcceptedEnvelope.CreateContext(message);
```

Prefer the fluent runtime API when the header set is dynamic. Prefer the generated path when the contract is stable and should fail at compile time if a required header is omitted.

## Relationship To Other Patterns

`Message<TPayload>` and `MessageContext` are not a replacement for a mediator, observer, or broker. They are shared metadata primitives for higher-level patterns:

- content-based routers can inspect headers and payloads
- routing slips can update itinerary headers
- sagas can correlate events by `CorrelationId`
- mailboxes can process envelopes sequentially
- idempotent receivers can use `IdempotencyKey`

## API

- <xref:PatternKit.Messaging.Message`1>
- <xref:PatternKit.Messaging.MessageHeaders>
- <xref:PatternKit.Messaging.MessageContext>
- <xref:PatternKit.Messaging.MessageHeaderNames>
- <xref:PatternKit.Generators.Messaging.GenerateMessageEnvelopeAttribute>
- <xref:PatternKit.Generators.Messaging.MessageEnvelopeHeaderAttribute>

## Example Source

- `src/PatternKit.Examples/Messaging/MessageEnvelopeExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MessageEnvelopeExampleTests.cs`
