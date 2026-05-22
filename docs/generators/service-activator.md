# Service Activator Generator

`[GenerateServiceActivator]` creates a typed `ServiceActivator<TRequest, TResponse>` factory.

```csharp
[GenerateServiceActivator(typeof(InventoryReservationRequest), typeof(InventoryReservationResult), FactoryName = "Create", ActivatorName = "inventory-reservation-activator")]
public static partial class InventoryActivator
{
    [ServiceActivatorHandler]
    private static Message<InventoryReservationResult> Reserve(Message<InventoryReservationRequest> request, MessageContext context)
        => Message<InventoryReservationResult>.Create(new(request.Payload.Sku, true, "allocated"));
}
```

The generated factory has no parameters, so the activator can be registered directly in `IServiceCollection` and injected into application services.

Diagnostics:

- `PKSVA001`: host type must be partial.
- `PKSVA002`: exactly one service activator handler is required.
- `PKSVA003`: service activator handler signature is invalid.
