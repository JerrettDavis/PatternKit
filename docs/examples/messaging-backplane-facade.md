# Messaging Backplane Facade

This demo shows how an application can build a MassTransit- or MediatR-shaped facade while keeping PatternKit in its intended role: pattern composition, not broker infrastructure.

Source:

- `src/PatternKit.Examples/Messaging/BackplaneFacadeDemo.cs`
- `test/PatternKit.Examples.Tests/Messaging/BackplaneFacadeDemoTests.cs`

## What PatternKit Provides

The demo composes existing PatternKit primitives:

- <xref:PatternKit.Messaging.Message`1> and <xref:PatternKit.Messaging.MessageContext> carry typed payloads, correlation, causation, reply, and idempotency headers.
- <xref:PatternKit.Messaging.Routing.ContentRouter`2> maps request payloads to backplane endpoints.
- <xref:PatternKit.Messaging.Routing.RecipientList`1> fans an event out to every matching transport subscription.
- <xref:PatternKit.Messaging.Mailboxes.Mailbox`1> serializes each subscriber inbox.
- <xref:PatternKit.Messaging.Reliability.IdempotentReceiver`2> suppresses duplicate commands and replays the completed response.
- An application-owned outbox records published envelopes before the transport dispatches them.

The broker remains application-owned. `IBackplaneTransport` is a small boundary:

```csharp
public interface IBackplaneTransport : IAsyncDisposable
{
    ValueTask<IAsyncDisposable> SubscribeAsync(
        string address,
        string subscriberName,
        BackplaneTransportHandler handler,
        CancellationToken cancellationToken = default);

    ValueTask<int> SendAsync(
        string address,
        BackplaneEnvelope envelope,
        CancellationToken cancellationToken = default);
}
```

The example uses `InMemoryBackplaneTransport` for deterministic tests. A production adapter could implement the same boundary over RabbitMQ exchanges, Azure Service Bus topics/queues, Postgres-backed tables and notifications, MQTT topics, or another transport.

## Application Startup

The demo now starts from a host builder, which is the shape a production application would usually expose from its composition root:

```csharp
await using var host = await BackplaneHost.Create()
    .UseTransport(() => transport)
    .UseOutbox(outbox)
    .UseIdempotencyStore(idempotency)
    .MapCommand<SubmitOrder, BackplaneOrderAccepted>(
        static (message, _) => message.Payload.CustomerTier == CustomerTier.Vip,
        "orders.priority")
    .MapDefaultCommand<SubmitOrder, BackplaneOrderAccepted>("orders.standard")
    .ReceiveEndpoint("orders.standard", endpoint =>
        endpoint.HandleCommand<SubmitOrder, BackplaneOrderAccepted>(services.AcceptStandardOrderAsync))
    .ReceiveEndpoint("billing-service", endpoint =>
        endpoint.Subscribe<BackplaneOrderSubmitted>("orders.submitted", services.CapturePaymentAsync))
    .ReceiveEndpoint("notification-service", endpoint =>
    {
        endpoint.Subscribe<PaymentDeclined>("payments.declined", services.NotifyPaymentDeclinedAsync);
        endpoint.Subscribe<ShipmentScheduled>("shipments.scheduled", services.NotifyShipmentScheduledAsync);
    })
    .BuildAsync(cancellationToken);
```

`BackplaneHost` owns the bus, typed client, transport, endpoint subscriptions, outbox, idempotency store, and topology metadata. Application code uses `host.Client`, while advanced integrations can still reach the lower-level `host.Bus`.

## Request/Reply

The client exposes a typed request/reply API:

```csharp
var accepted = await host.Client.RequestAsync<SubmitOrder, BackplaneOrderAccepted>(
    Message<SubmitOrder>
        .Create(new SubmitOrder("order-42", 90m, CustomerTier.Standard))
        .WithCorrelationId("corr-order-42")
        .WithIdempotencyKey("idem-order-42"),
    cancellationToken);
```

`BackplaneClient.RequestAsync<TRequest, TResponse>` creates a temporary reply address, enriches the message with a reply header, routes the command with the content router, sends it through the transport, and waits for the typed response. Duplicate requests with the same idempotency key replay the stored `BackplaneOrderAccepted` response without republishing `BackplaneOrderSubmitted`.

## Publish/Subscribe

The order service publishes an event through the outbox:

```csharp
await client.PublishAsync(
    "orders.submitted",
    new BackplaneOrderSubmitted(message.Payload.OrderId, message.Payload.Total, message.Payload.CustomerTier),
    context.Headers,
    token);
```

The transport uses a recipient list so every matching subscriber receives the envelope:

- Billing receives `orders.submitted`, captures or declines payment, and publishes payment events.
- Audit receives the same `orders.submitted` event independently.
- Fulfillment receives `payments.captured` and publishes `shipments.scheduled`.
- Notification receives `payments.declined` and `shipments.scheduled`.

Each subscriber runs behind a bounded mailbox, so stateful handlers process one message at a time with explicit backpressure.

## Transport Adapter Boundary

The application chooses the transport at startup:

```csharp
BackplaneHost.Create()
    .UseTransport(() => new RabbitMqBackplaneTransport(/* connection settings */));
```

`RabbitMqBackplaneTransport` is not part of PatternKit; it would be application infrastructure that implements `IBackplaneTransport`. The same host, routes, endpoints, command handlers, and event subscribers can run over any adapter that honors the transport contract.

## Tested Behavior

The tests assert that:

- Standard orders route to `orders.standard` and VIP orders route to `orders.priority`.
- The host builder configures transport, outbox, idempotency, endpoint topology, and the typed client surface.
- Duplicate commands replay the original response and do not duplicate outbox side effects.
- Published events fan out to independent services.
- Every event is recorded in the outbox before transport dispatch.
- Correlation IDs flow from the original command through payment, fulfillment, and notification services.

## Production Adapter Shape

PatternKit deliberately does not hide broker concerns. A real adapter should still own:

- Connection/session lifecycle.
- Broker-specific topology declaration.
- Serialization and content type policy.
- Retry, dead-letter, and poison-message rules.
- Durable inbox/outbox storage.
- Observability and operational metrics.

PatternKit keeps the application surface small and testable while leaving those infrastructure decisions explicit.
