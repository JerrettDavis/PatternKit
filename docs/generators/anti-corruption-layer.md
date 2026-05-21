# Anti-Corruption Layer Generator

The anti-corruption layer generator creates a strongly typed `AntiCorruptionLayer<TExternal, TDomain>` factory from declarative translator and validation attributes.

```csharp
[GenerateAntiCorruptionLayer(
    typeof(LegacyOrderDto),
    typeof(CommerceOrder),
    FactoryMethodName = "CreateGeneratedLayer",
    LayerName = "legacy-order-import",
    SourceSystem = "legacy-erp")]
public static partial class LegacyOrderAcl
{
    [AntiCorruptionTranslator]
    private static CommerceOrder Translate(LegacyOrderDto order)
        => new(order.OrderNumber.Trim(), order.GrossAmount, order.CustomerCode.Trim());

    [AntiCorruptionExternalRule("Only USD orders are imported.")]
    private static bool IsUsd(LegacyOrderDto order) => order.CurrencyCode == "USD";

    [AntiCorruptionDomainRule("Imported order total must be positive.")]
    private static bool HasPositiveTotal(CommerceOrder order) => order.TotalUsd > 0m;
}
```

The generated factory returns the same runtime layer as the fluent API.

## Rules

- The host type must be `partial`.
- Exactly one `[AntiCorruptionTranslator]` method is required.
- The translator must be `static`, return the domain type, and accept one external model parameter.
- External rules must be `static bool` methods with one external model parameter.
- Domain rules must be `static bool` methods with one domain model parameter.

Diagnostics use the `PKACL` prefix.
