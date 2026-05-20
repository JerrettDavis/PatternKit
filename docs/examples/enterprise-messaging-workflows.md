# Enterprise Messaging Workflow Suite

The messaging examples demonstrate in-process Enterprise Integration Patterns without pretending to be a broker, queue, or durable workflow engine. They show where PatternKit fits: message metadata, deterministic routing, workflow composition, idempotency boundaries, serialized processing, and generated factories for static topologies.

Example source:

- `src/PatternKit.Examples/Messaging`
- `test/PatternKit.Examples.Tests/Messaging`

## Pattern Map

| Pattern | Example source | What it demonstrates |
| --- | --- | --- |
| Message envelope/context | `MessageEnvelopeExample.cs` | Correlation, causation, idempotency, headers, typed payloads, and execution-scoped context. |
| Source-generated message envelope | `MessageEnvelopeExample.cs` | Required-header contract factories for stable integration boundaries. |
| Content router | `MessageRoutingExample.cs` | First-match routing of orders to named destinations. |
| Recipient list | `MessageRoutingExample.cs` | Fan-out to multiple interested recipients. |
| Splitter | `MessageRoutingExample.cs` | Splitting one aggregate message into line-level messages. |
| Aggregator | `MessageRoutingExample.cs` | Rejoining related messages into a summary. |
| Routing slip | `RoutingSlipExample.cs` | Ordered fulfillment steps with route progress stored in message headers. |
| Saga/process manager | `SagaExample.cs` | Typed message transitions over explicit saga state and completion rules. |
| Mailbox | `MailboxExample.cs` | Serialized async inbox processing with explicit lifecycle and error behavior. |
| Idempotent receiver | `ReliabilityExample.cs` | Duplicate detection around at-least-once message delivery. |
| Inbox/outbox | `ReliabilityExample.cs` | Explicit handoff records for durable integration boundaries owned by the application. |
| Source-generated dispatcher | `DispatcherExample.cs` | Compile-time mediator commands, notifications, streams, and paging. |
| Source-generated content router | `ContentRouterGeneratorExample.cs` | Attribute-driven content routing without runtime scanning. |
| Source-generated recipient list | `RecipientListGeneratorExample.cs` | Attribute-driven fan-out without runtime scanning. |
| Source-generated splitter and aggregator | `MessageRoutingExample.cs` | Attribute-driven split projection, correlation, completion, and result projection without runtime scanning. |
| Resilient checkout orchestration | `ResilientCheckoutDemo.cs` | Route selection, routing-slip execution, command compensation, and fallback routes. |
| Collaborating service mailboxes | `ServiceCollaborationMailboxDemo.cs` | Inventory, payment, shipping, and notification mailboxes collaborating over correlated messages. |
| Backplane facade | `BackplaneFacadeDemo.cs` | MassTransit/MediatR-shaped host builder, typed client, request/reply, and pub/sub over an application-owned transport boundary. |

## Workflow Shape

A production application usually combines these primitives in layers:

1. Accept or create a `Message<TPayload>` at the boundary with correlation, causation, and idempotency headers. Use generated envelope contracts when the header set is stable.
2. Route the message through a content router, recipient list, splitter, or routing slip.
3. Serialize stateful handlers through a mailbox when concurrency must be constrained.
4. Use a saga/process manager when multiple messages update long-running state.
5. Wrap handlers with an idempotent receiver and write outbox records before handing work to external infrastructure.

The examples keep each part small so the ownership boundary stays clear. PatternKit handles in-process composition; persistence, transport, retries across restarts, and exactly-once claims belong to the application and its infrastructure.

## Generated And Runtime Variants

Use runtime builders when the route table, itinerary, or transition set is built from configuration:

```csharp
var slip = RoutingSlip<FulfillmentOrder>.Create()
    .Step("reserve", ReserveInventory)
    .Step("ship", ShipOrder)
    .Build();
```

Use source generators when topology is stable and should be compile-time validated:

```csharp
[GenerateRoutingSlip(typeof(FulfillmentOrder))]
public static partial class FulfillmentSlip
{
    [RoutingSlipStep("reserve", 10)]
    private static Message<FulfillmentOrder> Reserve(Message<FulfillmentOrder> message, MessageContext context)
        => message;
}
```

The generated factories are AOT-friendly and do not scan assemblies. The runtime builders are better for user- or tenant-defined routing.

Generated splitter and aggregator contracts follow the same rule: use `[GenerateSplitter]` and `[SplitterProjection]` for a stable split projection, then `[GenerateAggregator]` with `[AggregatorCorrelation]`, `[AggregatorCompletion]`, and `[AggregatorProjection]` for the matching rejoin contract.

## Testing Guidance

The example tests use behavior-oriented assertions:

- Envelope tests assert metadata propagation and immutable message updates.
- Routing tests assert deterministic first-match and aggregation behavior.
- Routing-slip tests assert step order and header progress.
- Saga tests assert transition behavior and completion state.
- Mailbox tests assert serialized processing and lifecycle semantics.
- Reliability tests assert duplicate suppression and outbox record creation.
- Generator tests assert that generated factories compile and behave like the equivalent runtime builders.
- Resilient checkout tests assert rollback, fallback route selection, manual review, and side-effect boundaries.
- Mailbox collaboration tests assert service handoff, compensation, correlation propagation, and final notification outcomes.
- Backplane facade tests assert startup-style host configuration, routed request/reply, publish/subscribe fanout, outbox dispatch, idempotent replay, correlation propagation, and RabbitMQ/MQTT Testcontainers E2E delivery.

## Related Documentation

- [Messaging Patterns](../patterns/messaging/README.md)
- [Message Envelope and Context](../patterns/messaging/message-envelope.md)
- [Enterprise Message Routing](../patterns/messaging/message-routing.md)
- [Routing Slip](../patterns/messaging/routing-slip.md)
- [Saga / Process Manager](../patterns/messaging/saga.md)
- [Mailbox](../patterns/messaging/mailbox.md)
- [Idempotent Receiver, Inbox, and Outbox](../patterns/messaging/reliability.md)
- [Messaging Generators](../generators/messaging.md)
- [Generated Splitter And Aggregator](generated-splitter-aggregator.md)
- [Resilient Checkout and Collaborating Mailboxes](resilient-checkout-and-mailboxes.md)
- [Messaging Backplane Facade](messaging-backplane-facade.md)
