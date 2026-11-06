# Commerce Context Map Pattern

The commerce context map example shows how Catalog, Fulfillment, and Billing integrate without hiding the ownership boundary.

The example exposes:

- a fluent `ContextMapDescriptor`
- a generated descriptor from `GeneratedCommerceContextMap`
- a catalog-to-fulfillment translator
- `AddCommerceContextMapDemo()` for `IServiceCollection`

```csharp
var services = new ServiceCollection()
    .AddCommerceContextMapDemo();

using var provider = services.BuildServiceProvider();
var summary = provider.GetRequiredService<CommerceContextMapReporter>().Summarize();
```

The generated map is useful for architecture tests, operational diagnostics, and documentation pipelines that need to inspect integration relationships.
