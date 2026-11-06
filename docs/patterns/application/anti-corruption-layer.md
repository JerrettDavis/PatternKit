# Anti-Corruption Layer

An Anti-Corruption Layer protects a domain model from external schemas, naming, invariants, and operational shortcuts. It validates the external model, translates only accepted inputs, then validates the resulting domain model before anything downstream observes it.

PatternKit provides `AntiCorruptionLayer<TExternal, TDomain>` in `PatternKit.Application.AntiCorruption`.

```csharp
var layer = AntiCorruptionLayer<LegacyOrderDto, CommerceOrder>
    .Create("legacy-order-import")
    .FromSource("legacy-erp")
    .RequireExternal(static order => order.CurrencyCode == "USD", "Only USD orders are imported.")
    .TranslateWith(static order => new CommerceOrder(order.OrderNumber.Trim(), order.GrossAmount, order.CustomerCode.Trim()))
    .RequireDomain(static order => order.TotalUsd > 0m, "Imported order total must be positive.")
    .Build();

var result = layer.Translate(legacyOrder);
```

The result reports whether the import was accepted, the source system, the protected domain value, and any rejection reason.

## Production Notes

- Keep external DTOs outside the domain model and translate at the application boundary.
- Use external validation for schema drift, unsupported values, missing identifiers, and source-specific quirks.
- Use domain validation for invariants that must remain true after mapping and normalization.
- Return explicit rejection reasons so ingestion pipelines can route invalid input to diagnostics or remediation.
- Register the layer through DI beside the gateway/feed that reads the external system.

The legacy order example shows fluent and source-generated layer creation plus `IServiceCollection` registration for importing applications.
