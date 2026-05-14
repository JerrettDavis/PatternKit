# Resilient Checkout and Collaborating Mailboxes

This example pair shows PatternKit used in application-shaped messaging workflows, not isolated snippets:

- `ResilientCheckoutDemo` models a multi-step checkout process that chooses a route, executes ordered steps, compensates successful steps when a later step fails, and retries through fallback routes.
- `ServiceCollaborationMailboxDemo` models service mailboxes collaborating over messages while each service processes its own work serially.

Source:

- `src/PatternKit.Examples/Messaging/ResilientCheckoutDemo.cs`
- `src/PatternKit.Examples/Messaging/ServiceCollaborationMailboxDemo.cs`
- `test/PatternKit.Examples.Tests/Messaging/ResilientCheckoutDemoTests.cs`
- `test/PatternKit.Examples.Tests/Messaging/ServiceCollaborationMailboxDemoTests.cs`

## Resilient Checkout

The checkout demo combines:

- <xref:PatternKit.Messaging.Routing.ContentRouter`2> to pick an initial or fallback checkout route.
- <xref:PatternKit.Messaging.Routing.RoutingSlip`1> to execute ordered checkout steps.
- <xref:PatternKit.Behavioral.Command.Command`1> to model reversible units of work.
- <xref:PatternKit.Messaging.Message`1> and <xref:PatternKit.Messaging.MessageContext> to carry order/correlation data through the route.

The route selection is explicit:

```csharp
var router = ContentRouter<CheckoutAttempt, CheckoutRoute>.Create()
    .When((message, _) => message.Payload.Request.FraudHold)
    .Then((_, _) => CheckoutRoute.ManualReview)
    .When((message, _) => message.Payload.PreviousFailure == CheckoutFailureKind.InventoryUnavailable
                         && message.Payload.Request.AllowDropshipFallback)
    .Then((_, _) => CheckoutRoute.DropshipCard)
    .When((message, _) => message.Payload.PreviousFailure == CheckoutFailureKind.PaymentDeclined
                         && message.Payload.Request.GiftCardBalance >= message.Payload.Request.Total)
    .Then((_, _) => CheckoutRoute.PrimaryGiftCard)
    .Default((_, _) => CheckoutRoute.PrimaryCard)
    .Build();
```

Each route runs a routing slip:

```csharp
var slip = RoutingSlip<CheckoutContext>.Create()
    .Step("validate", Execute(ValidateCommand()))
    .Step("reserve-inventory", Execute(ReserveInventoryCommand(route.Inventory)))
    .Step("charge-payment", Execute(ChargePaymentCommand(route.Payment)))
    .Step("schedule-shipment", Execute(ScheduleShipmentCommand(route.Inventory)))
    .Build();
```

The commands are reversible. If inventory reserve succeeds but payment later declines, the checkout context walks the executed command stack in reverse and releases the reservation before trying the gift-card route:

```csharp
internal void Compensate()
{
    while (_executed.Count > 0)
    {
        var command = _executed.Pop();
        if (command.TryUndo(this, out var undo))
            undo.GetAwaiter().GetResult();
    }
}
```

The tests cover production-relevant paths:

- Primary warehouse + card succeeds.
- Primary inventory unavailable retries through dropship.
- Card payment decline releases inventory and retries gift card when balance is available.
- Fraud hold goes directly to manual review without side effects.
- Unrecoverable fulfillment failure ends as manual review.

This is still in-process orchestration. A real commerce system would persist checkout state, idempotency keys, and outbox records around the route execution before crossing process boundaries.

## Collaborating Service Mailboxes

The mailbox demo models four services:

- Inventory mailbox reserves or releases reservations.
- Payment mailbox captures payment or sends a release command when payment is declined.
- Shipping mailbox schedules fulfillment after payment capture.
- Notification mailbox emits final customer/application notifications.

Each service owns a `Mailbox<TCommand>` and processes commands serially. Services collaborate by posting messages to each other:

```csharp
payments = Mailbox<PaymentCommand>.Create(async (message, context, cancellationToken) =>
{
    if (message.Payload.Amount > 100m)
    {
        await inventory.PostAsync(
            Message<InventoryCommand>.Create(InventoryCommand.Release(message.Payload.OrderId)),
            context,
            cancellationToken);

        await notification.PostAsync(
            Message<NotificationCommand>.Create(new NotificationCommand(message.Payload.OrderId, "payment-declined")),
            context,
            cancellationToken);
        return;
    }

    await shipping.PostAsync(
        Message<ShippingCommand>.Create(new ShippingCommand(message.Payload.OrderId)),
        context,
        cancellationToken);
})
.Bounded(16, MailboxBackpressurePolicy.Wait)
.OnError(MailboxErrorPolicy.Continue)
.Build();
```

The demo sends two orders:

- `order-ok` reserves inventory, captures payment, schedules shipping, and emits a fulfilled notification.
- `order-declined` reserves inventory, fails payment, releases inventory, and emits a payment-declined notification.

Correlation IDs flow through the service posts via `MessageContext`, so downstream services can log and publish events under the same checkout correlation.

## Why These Patterns Fit

Use this shape when the application needs deterministic in-process coordination but does not need PatternKit to become infrastructure:

- Content routers choose routes from explicit state and previous failures.
- Routing slips keep ordered work visible and testable.
- Commands express compensating actions beside the work they reverse.
- Mailboxes serialize stateful service handlers and make backpressure explicit.
- Message headers and context carry correlation without global state.

For durable workflows, persist state before and after route execution, write outbox records for external messages, and use an external queue or broker for cross-process delivery.
