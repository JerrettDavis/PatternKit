# Product Search Rate Limiting

This example models a production product search endpoint where each tenant has an independent request budget. It demonstrates the same throttling rule through:

- a fluent `RateLimitPolicy<SearchResponse>`
- a source-generated rate-limit policy factory
- an `IServiceCollection` extension that imports the demo into a standard .NET host

```csharp
var services = new ServiceCollection();
services.AddProductSearchRateLimitingDemo();

using var provider = services.BuildServiceProvider();
var search = provider.GetRequiredService<ProductSearchRateLimitService>();

var first = await search.SearchAsync("tenant-a", "boots");
var second = await search.SearchAsync("tenant-a", "jackets");
var third = await search.SearchAsync("tenant-a", "hats");
```

The registered demo uses the generated policy path and a scripted search service. Applications can replace `IProductSearchService` with their own implementation while keeping the same policy registration shape.

The accompanying TinyBDD tests validate the fluent path, the generated path, tenant partitioning, rejected overflow requests, and the DI integration.
