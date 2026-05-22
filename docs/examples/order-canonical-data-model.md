# Order Canonical Data Model

The order canonical data model example normalizes partner and marketplace order contracts into `CanonicalCommerceOrder`.

```csharp
services.AddCanonicalOrderDataModelDemo();

var service = provider.GetRequiredService<CanonicalOrderImportService>();
var summary = service.ImportPartnerOrder(new PartnerOrderDocument("P-100", 42.50m, "usd"));
```

The example includes fluent and source-generated construction plus an importable `IServiceCollection` extension for standard .NET hosts.
