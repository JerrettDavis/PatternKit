# Messaging Generators

PatternKit includes seven messaging-oriented source generators:

- <xref:PatternKit.Generators.Messaging.GenerateDispatcherAttribute> for source-generated mediator dispatchers.
- <xref:PatternKit.Generators.Messaging.GenerateMessageEnvelopeAttribute> for required message-envelope contracts.
- <xref:PatternKit.Generators.Messaging.GenerateContentRouterAttribute> for content-based message routers.
- <xref:PatternKit.Generators.Messaging.GenerateRecipientListAttribute> for recipient-list fan-out.
- <xref:PatternKit.Generators.Messaging.GenerateSplitterAttribute> and <xref:PatternKit.Generators.Messaging.GenerateAggregatorAttribute> for split/rejoin routing.
- <xref:PatternKit.Generators.Messaging.GenerateRoutingSlipAttribute> for ordered routing-slip factories.
- <xref:PatternKit.Generators.Messaging.GenerateSagaAttribute> for typed saga/process-manager factories.

Use these generators when the message topology is known at compile time and should remain explicit, AOT-friendly, and validated by the compiler. They generate factories and fluent builders; they do not discover handlers from assemblies at runtime and they do not replace brokers, durable queues, or workflow engines.

## Generated Dispatcher

The dispatcher generator emits a mediator-style dispatcher from an assembly-level attribute:

```csharp
using PatternKit.Generators.Messaging;

[assembly: GenerateDispatcher(
    Namespace = "MyApp.Messaging",
    Name = "AppDispatcher")]
```

The generated dispatcher supports commands, notifications, streams, and pipeline behaviors without a runtime dependency on PatternKit. See [Dispatcher Generator](dispatcher.md) for the complete API, diagnostics, and examples.

Example source:

- `src/PatternKit.Examples/Messaging/DispatcherExample.cs`
- `src/PatternKit.Examples/MediatorComprehensiveDemo/ComprehensiveDemo.cs`
- `test/PatternKit.Examples.Tests/Messaging/DispatcherExampleTests.cs`

## Generated Message Envelope

`[GenerateMessageEnvelope]` creates typed factories for message contracts that require a stable set of headers:

```csharp
using PatternKit.Generators.Messaging;

[GenerateMessageEnvelope(typeof(OrderAccepted), FactoryName = "CreateAccepted")]
[MessageEnvelopeHeader("message-id", typeof(string), ParameterName = "messageId")]
[MessageEnvelopeHeader("correlation-id", typeof(string), ParameterName = "correlationId")]
[MessageEnvelopeHeader("tenant-id", typeof(string), ParameterName = "tenantId")]
public static partial class OrderAcceptedEnvelope;
```

The generated factory returns `Message<TPayload>` and writes every required header. It also emits a context factory so the same contract can start routing, saga, mailbox, or reliability workflows without manual `MessageContext.From(...)` boilerplate.

Example source:

- `src/PatternKit.Examples/Messaging/MessageEnvelopeExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MessageEnvelopeExampleTests.cs`

## Generated Content Router

`[GenerateContentRouter]` creates a `ContentRouter<TPayload, TResult>` factory from static route methods:

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateContentRouter(typeof(Order), typeof(string))]
public static partial class OrderRouter
{
    private static bool IsWholesale(Message<Order> message, MessageContext context)
        => message.Payload.Channel == "wholesale";

    [ContentRoute("wholesale", 10, nameof(IsWholesale))]
    private static string Wholesale(Message<Order> message, MessageContext context)
        => "wholesale";

    [ContentRouteDefault]
    private static string Default(Message<Order> message, MessageContext context)
        => "default";
}
```

Routes are ordered by `ContentRouteAttribute.Order`, then by name. Duplicate route names or duplicate route orders are diagnostics because a content router must remain deterministic.

Example source:

- `src/PatternKit.Examples/Messaging/ContentRouterGeneratorExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/ContentRouterGeneratorExampleTests.cs`

## Generated Routing Slip

`[GenerateRoutingSlip]` emits a factory for a named itinerary over `Message<TPayload>`:

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateRoutingSlip(typeof(FulfillmentOrder))]
public static partial class FulfillmentSlip
{
    [RoutingSlipStep("reserve", 10)]
    private static Message<FulfillmentOrder> Reserve(Message<FulfillmentOrder> message, MessageContext context)
        => message.WithPayload(message.Payload with { Status = "reserved" });

    [RoutingSlipStep("ship", 20)]
    private static Message<FulfillmentOrder> Ship(Message<FulfillmentOrder> message, MessageContext context)
        => message.WithPayload(message.Payload with { Status = "shipped" });
}
```

Generated routing slips are useful when the steps are static and route order should be validated at compile time. Runtime routing slips remain better when tenant configuration or user input defines the itinerary.

Example source:

- `src/PatternKit.Examples/Messaging/RoutingSlipExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/RoutingSlipExampleTests.cs`

## Generated Recipient List

`[GenerateRecipientList]` creates a `RecipientList<TPayload>` or `AsyncRecipientList<TPayload>` factory from static recipient methods:

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

The generator orders recipients by `RecipientListRecipientAttribute.Order`, validates predicates and handlers, and emits deterministic diagnostics for missing recipients, non-partial host types, invalid signatures, and duplicate names or order values.

Example files:

- `src/PatternKit.Examples/Messaging/RecipientListGeneratorExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/RecipientListGeneratorExampleTests.cs`

## Generated Splitter And Aggregator

`[GenerateSplitter]` creates a `Splitter<TPayload, TItem>` factory from one static projection method. `[GenerateAggregator]` creates an `Aggregator<TKey, TItem, TResult>` factory from static correlation, completion, and projection methods:

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateSplitter(typeof(Order), typeof(OrderLine), FactoryName = "CreateLineSplitter")]
public static partial class OrderSplitter
{
    [SplitterProjection]
    private static IEnumerable<OrderLine> ProjectLines(Message<Order> message, MessageContext context)
        => message.Payload.Lines;
}

[GenerateAggregator(typeof(string), typeof(OrderLine), typeof(decimal), FactoryName = "CreateLineTotal")]
public static partial class OrderLineAggregator
{
    [AggregatorCorrelation]
    private static string Correlate(Message<OrderLine> message, MessageContext context)
        => message.Headers.CorrelationId ?? message.Payload.OrderId;

    [AggregatorCompletion]
    private static bool Complete(string key, IReadOnlyList<Message<OrderLine>> messages, MessageContext context)
        => messages.Count == 2;

    [AggregatorProjection]
    private static decimal Project(string key, IReadOnlyList<Message<OrderLine>> messages, MessageContext context)
        => messages.Sum(message => message.Payload.Amount);
}
```

Use generated splitter/aggregator factories when the split projection and rejoin contract are stable. Use runtime builders when completion rules depend on tenant configuration or runtime-discovered topology.

Example files:

- `src/PatternKit.Examples/Messaging/MessageRoutingExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/MessageRoutingExampleTests.cs`

## Generated Saga

`[GenerateSaga]` emits a process-manager factory from typed transition methods:

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateSaga(typeof(OrderSagaState))]
public static partial class OrderSaga
{
    [SagaStep(typeof(OrderSubmitted), 10)]
    private static OrderSagaState Submitted(OrderSagaState state, Message<OrderSubmitted> message, MessageContext context)
        => state with { OrderId = message.Payload.OrderId, Submitted = true };

    [SagaCompleteWhen]
    private static bool IsComplete(OrderSagaState state)
        => state.Submitted && state.Paid;
}
```

Use generated sagas for in-process orchestration where transitions are explicit and state is owned by the caller. Persist saga state and outbox records outside PatternKit when the workflow must survive process restarts.

Example source:

- `src/PatternKit.Examples/Messaging/SagaExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/SagaExampleTests.cs`

## Diagnostics

| ID | Generator | Meaning |
| --- | --- | --- |
| `PKDSP001`-`PKDSP004` | Dispatcher | Invalid dispatcher configuration or handler registration. |
| `PKME001`-`PKME004` | Message Envelope | Non-partial host, missing headers, invalid header configuration, or duplicate names. |
| `PKCR001`-`PKCR005` | Content Router | Non-partial host, missing routes, invalid signatures, duplicate defaults, or duplicate route identity. |
| `PKRL001`-`PKRL004` | Recipient List | Non-partial host, missing recipients, invalid signatures, or duplicate recipient identity. |
| `PKSA001`-`PKSA006` | Splitter / Aggregator | Non-partial host, missing contract methods, invalid signatures, or invalid duplicate policy. |
| `PKRS001`-`PKRS003` | Routing Slip | Non-partial host, missing steps, or invalid step signatures. |
| `PKSG001`-`PKSG004` | Saga | Non-partial host, missing transitions, invalid transition signatures, or invalid completion checks. |

## Related Runtime Patterns

- [Message Envelope and Context](../patterns/messaging/message-envelope.md)
- [Enterprise Message Routing](../patterns/messaging/message-routing.md)
- [Routing Slip](../patterns/messaging/routing-slip.md)
- [Saga / Process Manager](../patterns/messaging/saga.md)
- [Mailbox](../patterns/messaging/mailbox.md)
- [Idempotent Receiver, Inbox, and Outbox](../patterns/messaging/reliability.md)
