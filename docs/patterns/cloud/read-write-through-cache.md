# Read-Through and Write-Through Cache

PatternKit provides `ReadWriteThroughCachePolicy<TResult>` in `PatternKit.Cloud.ReadWriteThroughCache`.

Use it when the application should not hand-roll cache orchestration around repositories. `ReadThroughAsync` checks the cache first, loads from the origin on a miss, then stores the loaded value. `WriteThroughAsync` persists to the origin first and updates the cache only after the write succeeds.

```csharp
var policy = ReadWriteThroughCachePolicy<CatalogProduct>
    .Create("product-catalog-read-write-through")
    .WithTimeToLive(TimeSpan.FromMinutes(5))
    .Build();
```

The product catalog example demonstrates fluent and source-generated policy creation, plus `IServiceCollection` registration for applications that want container-owned policies and services.
