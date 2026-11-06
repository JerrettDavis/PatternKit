# Product Catalog Read-Through and Write-Through Cache

This example models a product catalog repository where reads and writes are coordinated through one cache policy.

- `ReadThroughAsync` loads missing products from the repository and caches them.
- `WriteThroughAsync` saves the product to the repository before updating the cache.
- `AddProductCatalogReadWriteThroughDemo()` registers the generated policy, repository, service, and runner with `IServiceCollection`.

```csharp
var services = new ServiceCollection();
services.AddProductCatalogReadWriteThroughDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var runner = provider.GetRequiredService<ProductCatalogReadWriteThroughDemoRunner>();
var results = await runner.RunAsync();
```

The fluent and source-generated paths produce the same `ReadWriteThroughCachePolicy<CatalogProduct>` surface.
