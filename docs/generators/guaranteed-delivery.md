# Guaranteed Delivery Generator

`[GenerateGuaranteedDelivery]` emits a typed queue factory backed by `InMemoryGuaranteedDeliveryStore<TPayload>`.

```csharp
[GenerateGuaranteedDelivery(
    typeof(ShipmentDispatchCommand),
    FactoryName = "Create",
    QueueName = "shipment-guaranteed-delivery",
    LeaseMilliseconds = 30000,
    MaxDeliveryAttempts = 3)]
public static partial class GeneratedShipmentGuaranteedDeliveryQueue;
```

The generated path is useful for demos, tests, and applications that want a repeatable queue shape without handwritten builder code. Production systems can keep the fluent path and provide a durable `IGuaranteedDeliveryStore<TPayload>` implementation through DI.
