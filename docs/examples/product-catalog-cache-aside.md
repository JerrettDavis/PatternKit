# Product Catalog Cache-Aside

This example models a production product catalog read model where active products can be cached, but inactive products remain origin reads. It demonstrates the same cache-aside rule through:

- a fluent `CacheAsidePolicy<ProductReadModel>`
- a source-generated cache-aside policy factory
- an `IServiceCollection` extension that imports the demo into a standard .NET host

```csharp
var services = new ServiceCollection();
services.AddProductCatalogCacheAsideDemo();

using var provider = services.BuildServiceProvider();
var catalog = provider.GetRequiredService<ProductCatalogCacheAsideService>();

var first = await catalog.FindAsync("SKU-42");
var second = await catalog.FindAsync("SKU-42");
```

The registered demo uses the generated policy path and a scripted product catalog repository. Applications can replace `IProductCatalogRepository` with their own implementation while keeping the same policy registration shape.

The accompanying TinyBDD tests validate the fluent path, the generated path, inactive-product cache predicates, and the DI integration.
