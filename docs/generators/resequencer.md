# Resequencer Generator

`[GenerateResequencer]` creates a typed `Resequencer<TPayload>` factory.

```csharp
[GenerateResequencer(typeof(ShipmentEvent), FactoryName = "Create", Name = "shipment-events")]
public static partial class ShipmentEvents
{
    [ResequencerSequence]
    private static long Select(Message<ShipmentEvent> message, MessageContext context)
        => message.Payload.Sequence;
}
```

The selector must be a static method returning `long` with `Message<TPayload>` and `MessageContext` parameters. Set `StartsAt` when the first expected sequence is not `1`.

Diagnostics:

- `PKRSEQ001`: host type must be partial.
- `PKRSEQ002`: exactly one sequence selector is required.
- `PKRSEQ003`: sequence selector signature is invalid.
