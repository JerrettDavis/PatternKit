# Canonical Data Model

Canonical Data Model normalizes partner, vendor, or application-specific records into one application-owned contract.

```csharp
var model = CanonicalDataModel<CanonicalCommerceOrder>
    .Create("commerce-orders")
    .From<PartnerOrderDocument>("partner-orders", order => new(
        order.ExternalOrderId,
        order.Amount,
        order.CurrencyCode.ToUpperInvariant()))
    .Build();

var result = model.Normalize(partnerOrder);
```

Use it when multiple integrations need to feed routers, sagas, service activators, or application services through the same stable payload shape. The runtime path reports missing adapters and mapper failures as explicit results.

The source-generated path uses `[GenerateCanonicalDataModel]` and `[CanonicalDataModelMapper]`. Import the example through `AddCanonicalOrderDataModelDemo()` or `AddPatternKitExamples()`.
