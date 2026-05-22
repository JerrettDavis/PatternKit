# Supplier Quote Scatter-Gather

The supplier quote example requests prices from multiple suppliers and aggregates the best quote.

```csharp
services.AddSupplierQuoteScatterGatherDemo();

var service = provider.GetRequiredService<SupplierQuoteService>();
var summary = service.RequestQuotes(new SupplierQuoteRequest("SKU-100", 120, requiresColdChain: false));
```

The example includes fluent and source-generated construction, recipient predicates, best-price aggregation, and `IServiceCollection` registration.
