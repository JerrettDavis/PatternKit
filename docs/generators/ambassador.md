# Ambassador Generator

`[GenerateAmbassador]` creates a typed `Ambassador<TRequest, TResponse>` factory from outbound call, policy, transform, telemetry, and fallback methods.

```csharp
[GenerateAmbassador(typeof(InventoryAmbassadorRequest), typeof(InventoryAmbassadorResponse), AmbassadorName = "inventory-ambassador")]
public static partial class InventoryAmbassador
{
    [AmbassadorTransform]
    private static InventoryAmbassadorRequest Normalize(InventoryAmbassadorRequest request) => request;

    [AmbassadorCall]
    private static InventoryAmbassadorResponse Call(AmbassadorContext<InventoryAmbassadorRequest> ctx)
        => new(ctx.Request.Sku, "available", "inventory-api");
}
```

Diagnostics:

- `PKAMB001`: host type must be partial.
- `PKAMB002`: exactly one outbound call handler is required.
- `PKAMB003`: transform, policy, telemetry, call, or fallback signature is invalid.
- `PKAMB004`: telemetry names must be unique.
