# Event Notification Generator

`[GenerateEventNotification]` creates a typed `EventNotification<TEvent, TKey>` factory from key, rule, correlation, and metadata methods.

```csharp
[GenerateEventNotification(typeof(OrderAccepted), typeof(string), NotificationName = "order-accepted")]
public static partial class OrderAcceptedNotification
{
    [EventNotificationRule]
    private static bool ShouldNotify(OrderAccepted evt) => evt.NotifySubscribers;

    [EventNotificationKey]
    private static string Key(OrderAccepted evt) => evt.OrderId;

    [EventNotificationCorrelation]
    private static string Correlation(OrderAccepted evt) => evt.CorrelationId;

    [EventNotificationMetadata("source")]
    private static string Source(OrderAccepted evt) => evt.Source;
}
```

Diagnostics:

- `PKEN001`: host type must be partial.
- `PKEN002`: exactly one key selector is required.
- `PKEN003`: selector, rule, correlation, or metadata signature is invalid.
- `PKEN004`: metadata names must be unique.
