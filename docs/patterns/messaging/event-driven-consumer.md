# Event-Driven Consumer

Event-Driven Consumer reacts when application code delivers a message to the consumer.

```csharp
var consumer = EventDrivenConsumer<OrderAcceptedEvent>
    .Create("order-accepted-consumer")
    .Handle("audit", (message, context) =>
    {
        audit.Append(message.Payload.OrderId);
        return EventDrivenConsumerHandlerResult.Success("audit");
    })
    .Build();

var result = consumer.Accept(Message<OrderAcceptedEvent>.Create(orderAccepted));
```

Use it when the message arrival cadence is controlled by a broker callback, background service, webhook, in-memory bus, or application event source. The runtime path records handler failures and can either stop on the first failure or continue invoking remaining handlers.

The source-generated path uses `[GenerateEventDrivenConsumer]` and `[EventDrivenConsumerHandler]`. Import the order event example through `AddOrderEventDrivenConsumerDemo()` or `AddPatternKitExamples()`.
