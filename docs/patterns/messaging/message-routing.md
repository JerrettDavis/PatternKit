# Enterprise Message Routing

PatternKit messaging routing primitives model common Enterprise Integration Patterns for in-process workflows. They are small fluent builders over delegates, designed to compose with `Message<TPayload>` and `MessageContext`.

These APIs do not provide broker delivery, persistence, or cross-process guarantees. Use them behind your transport, queue consumer, API handler, background worker, or generated dispatcher when the message is already in your process.

## Content-Based Router

`ContentRouter<TPayload, TResult>` selects the first matching route.

```csharp
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

var router = ContentRouter<Order, string>.Create()
    .When((m, _) => m.Payload.Total > 100m).Then((_, _) => "priority")
    .Default((_, _) => "standard")
    .Build();

var route = router.Route(Message<Order>.Create(new Order("order-1", 150m)));
```

Use `AsyncContentRouter<TPayload, TResult>` when predicates or handlers need async work.

## Recipient List

`RecipientList<TPayload>` sends a message to every matching recipient in registration order and returns the names that received it.

```csharp
var delivered = new List<string>();

var recipients = RecipientList<Order>.Create()
    .To("audit", (_, _) => delivered.Add("audit"))
    .When("billing", (m, _) => m.Payload.Total > 0m)
    .Then((_, _) => delivered.Add("billing"))
    .Build();

var result = recipients.Dispatch(Message<Order>.Create(new Order("order-1", 150m)));
```

Use `AsyncRecipientList<TPayload>` for async recipient handlers.

Use `[GenerateRecipientList]` when the recipient map is part of application structure and should be compiled into a strongly typed factory:

```csharp
[GenerateRecipientList(typeof(Order))]
public static partial class OrderRecipients
{
    private static bool IsPriority(Message<Order> message, MessageContext context)
        => message.Payload.Priority == "priority";

    [RecipientListRecipient("priority-audit", 10, nameof(IsPriority))]
    private static void PriorityAudit(Message<Order> message, MessageContext context)
    {
        // deliver to audit sink
    }
}
```

The generated factory returns the same `RecipientList<TPayload>` runtime type as the fluent API.

## Splitter

`Splitter<TPayload, TItem>` turns one message into item messages. Child messages preserve the parent headers. When the parent has a message id and no causation id, child messages set `CausationId` to the parent `MessageId`.

```csharp
var splitter = Splitter<Order, LineItem>.Create()
    .Use((m, _) => m.Payload.Lines)
    .Build();

var lineMessages = splitter.Split(orderMessage);
```

## Aggregator

`Aggregator<TKey, TItem, TResult>` groups messages in memory until a completion policy is satisfied, then projects the completed group into a result and removes it from the open groups.

```csharp
var aggregator = Aggregator<string, LineItem, decimal>.Create()
    .KeyBy((m, _) => m.Headers.CorrelationId ?? "missing")
    .CompleteWhen((_, messages, _) => messages.Count == 2)
    .Project((_, messages, _) => messages.Sum(m => m.Payload.Amount))
    .Build();

var result = aggregator.Add(lineMessage);
if (result.Completed)
{
    Console.WriteLine(result.Result);
}
```

Duplicate message ids are ignored by default. Use `DuplicateMessagePolicy.Replace` or `DuplicateMessagePolicy.Include` when a workflow needs different behavior.

## Choosing Boundaries

Use these primitives for:

- deterministic in-process routing
- composing handlers behind a queue consumer or HTTP endpoint
- testable routing rules without broker dependencies
- dynamic flows that still need clear, explicit code

Use external infrastructure for:

- durable queues
- retry after process restart
- competing consumers
- exactly-once or at-least-once delivery contracts
- transactional outbox persistence

## API

- <xref:PatternKit.Messaging.Routing.ContentRouter`2>
- <xref:PatternKit.Messaging.Routing.AsyncContentRouter`2>
- <xref:PatternKit.Messaging.Routing.RecipientList`1>
- <xref:PatternKit.Messaging.Routing.AsyncRecipientList`1>
- <xref:PatternKit.Generators.Messaging.GenerateRecipientListAttribute>
- <xref:PatternKit.Generators.Messaging.RecipientListRecipientAttribute>
- <xref:PatternKit.Messaging.Routing.Splitter`2>
- <xref:PatternKit.Messaging.Routing.Aggregator`3>
- <xref:PatternKit.Messaging.Routing.AggregationResult`2>
- <xref:PatternKit.Messaging.Routing.DuplicateMessagePolicy>

## Example Source

- `src/PatternKit.Examples/Messaging/MessageRoutingExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MessageRoutingExampleTests.cs`
