# Canonical Data Model Generator

`[GenerateCanonicalDataModel]` creates a typed `CanonicalDataModel<TCanonical>` factory from a source-to-canonical mapper.

```csharp
[GenerateCanonicalDataModel(typeof(PartnerOrderDocument), typeof(CanonicalCommerceOrder), ModelName = "commerce-orders", AdapterName = "partner-orders")]
public static partial class PartnerOrderCanonicalModel
{
    [CanonicalDataModelMapper]
    private static CanonicalCommerceOrder Map(PartnerOrderDocument order) => new(order.ExternalOrderId, order.Amount, "USD");
}
```

The generated factory is parameterless, so applications can register the canonical model directly in `IServiceCollection`.

Diagnostics:

- `PKCDM001`: host type must be partial.
- `PKCDM002`: exactly one mapper is required.
- `PKCDM003`: mapper signature is invalid.
