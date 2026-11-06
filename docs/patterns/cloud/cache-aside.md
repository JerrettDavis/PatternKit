# Cache-Aside

Cache-Aside keeps a cache beside the origin data source. Callers check the cache first, load from the origin on misses, and populate the cache only when the loaded value should be reused.

PatternKit provides `CacheAsidePolicy<TResult>` in `PatternKit.Cloud.CacheAside`.

```csharp
var policy = CacheAsidePolicy<ProductReadModel>
    .Create("product-catalog")
    .WithTimeToLive(TimeSpan.FromMinutes(5))
    .CacheWhen(static product => product.Active)
    .Build();

var result = await policy.GetOrLoadAsync(
    "SKU-42",
    ct => productRepository.FindAsync("SKU-42", ct),
    cancellationToken);
```

The policy returns `CacheAsideResult<TResult>` so callers can inspect the value, whether it was found, and whether it came from the cache.

## Production Notes

- Cache read models or reference data that tolerate bounded staleness.
- Use `CacheWhen` to avoid caching deleted, inactive, or incomplete values.
- Set TTLs based on data volatility and operational tolerance.
- Invalidate cache entries after writes that affect cached keys.
- Replace the default in-memory store with a distributed implementation when multiple app instances must share entries.

The product catalog cache-aside example shows both fluent and source-generated policy creation, plus `IServiceCollection` registration for importing applications.
