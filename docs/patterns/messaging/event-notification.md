# Event Notification

Event Notification publishes a compact signal that something happened, usually with an identifier and correlation metadata rather than the full state payload.

```csharp
var notification = EventNotification<OrderAccepted, string>
    .Create("order-accepted")
    .When(evt => evt.NotifySubscribers)
    .WithKey(evt => evt.OrderId)
    .WithCorrelation(evt => evt.CorrelationId)
    .WithMetadata("source", evt => evt.Source)
    .Build();

var result = notification.Notify(orderAccepted);
```

Use it when subscribers can react to a lightweight event and only fetch more detail when their workflow requires it. The runtime path supports dispatch predicates, correlation IDs, metadata, and explicit skipped or failed results.

The source-generated path uses `[GenerateEventNotification]`, `[EventNotificationKey]`, `[EventNotificationRule]`, `[EventNotificationCorrelation]`, and `[EventNotificationMetadata]`. Import the example through `AddOrderEventNotificationDemo()` or `AddPatternKitExamples()`.
