# Guaranteed Delivery

Guaranteed Delivery stores a message before work begins, leases it to a worker, and only removes it from the active flow after acknowledgement. Use it when a shipment, payment, notification, or integration command must survive transient worker failures.

PatternKit exposes `GuaranteedDeliveryQueue<TPayload>` with pluggable `IGuaranteedDeliveryStore<TPayload>` storage. The in-memory store is useful for tests and demos; production applications can implement the store with a database, broker, or durable queue.

```csharp
var queue = GuaranteedDeliveryQueue<ShipmentDispatchCommand>
    .Create(new InMemoryGuaranteedDeliveryStore<ShipmentDispatchCommand>())
    .Name("shipment-guaranteed-delivery")
    .LeaseDuration(TimeSpan.FromSeconds(30))
    .MaxDeliveryAttempts(3)
    .Build();
```

The queue supports enqueue, lease/receive, acknowledge, release for retry, dead-letter, and snapshot operations.
