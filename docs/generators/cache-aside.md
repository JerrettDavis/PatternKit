# Cache-Aside Generator

The cache-aside generator creates a strongly typed `CacheAsidePolicy<TResult>` factory from declarative attributes. It is useful when cache TTLs and cacheability rules should live beside the read model contract while still producing the same runtime policy as the fluent API.

```csharp
[GenerateCacheAsidePolicy(
    typeof(ProductReadModel),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "product-catalog",
    TimeToLiveMilliseconds = 300000)]
public static partial class ProductCatalogCacheAsidePolicy
{
    [CacheAsidePredicate]
    private static bool ShouldCache(ProductReadModel product)
        => product.Active;
}
```

The generated factory returns `PatternKit.Cloud.CacheAside.CacheAsidePolicy<ProductReadModel>` and applies the optional cache predicate.

## Rules

- The host type must be `partial`.
- `TimeToLiveMilliseconds` must be non-negative.
- A value of `0` means no expiration.
- Cache predicates must be `static bool` methods with one `TResult` parameter.
- Only one cache predicate can be declared.

Diagnostics use the `PKCA` prefix.
