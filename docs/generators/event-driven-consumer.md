# Event-Driven Consumer Generator

`[GenerateEventDrivenConsumer]` creates a typed `EventDrivenConsumer<TPayload>` factory.

```csharp
[GenerateEventDrivenConsumer(typeof(OrderAcceptedEvent), FactoryName = "Create", ConsumerName = "order-accepted-consumer")]
public static partial class OrderAcceptedConsumer
{
    [EventDrivenConsumerHandler("audit")]
    private static EventDrivenConsumerHandlerResult Audit(Message<OrderAcceptedEvent> message, MessageContext context)
        => EventDrivenConsumerHandlerResult.Success("audit");
}
```

Handler methods must be static, return `EventDrivenConsumerHandlerResult`, and accept `Message<TPayload>` plus `MessageContext`.

Diagnostics:

- `PKEVT001`: host type must be partial.
- `PKEVT002`: at least one event-driven consumer handler is required.
- `PKEVT003`: event-driven consumer handler signature is invalid.
