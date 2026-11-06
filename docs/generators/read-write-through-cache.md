# Read-Through and Write-Through Cache Generator

The read/write-through cache generator creates a strongly typed `ReadWriteThroughCachePolicy<TResult>` factory from `[GenerateReadWriteThroughCachePolicy]`.

```csharp
[GenerateReadWriteThroughCachePolicy(
    typeof(CatalogProduct),
    FactoryMethodName = "CreateGenerated",
    PolicyName = "product-catalog-read-write-through",
    TimeToLiveMilliseconds = 300000)]
public static partial class GeneratedProductCatalogReadWriteThroughPolicy;
```

The generated factory returns the same runtime policy as the fluent API and applies the configured TTL.
