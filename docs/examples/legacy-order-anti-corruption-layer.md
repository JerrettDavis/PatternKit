# Legacy Order Anti-Corruption Layer

This example models a legacy ERP order feed imported into a commerce domain. The anti-corruption layer keeps legacy DTO shape, currency codes, whitespace, and customer identifiers out of the domain model.

It demonstrates:

- a fluent `AntiCorruptionLayer<LegacyOrderDto, CommerceOrder>`
- a source-generated anti-corruption layer factory
- an `IServiceCollection` extension that imports the demo into a standard .NET host

```csharp
var services = new ServiceCollection();
services.AddLegacyOrderAntiCorruptionDemo();

using var provider = services.BuildServiceProvider();
var importer = provider.GetRequiredService<LegacyOrderImportService>();

var result = await importer.ImportAsync("ORD-100");
```

Applications can replace `ILegacyOrderFeed` with their own gateway while keeping the generated layer registration. The accompanying TinyBDD tests validate accepted imports, rejected model drift, and DI composition.
