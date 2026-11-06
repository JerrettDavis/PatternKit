# Cache Stampede Protection Generator

The Cache Stampede Protection generator creates a strongly typed factory for `CacheStampedeProtectionPolicy<TResult>` from a partial host type.

```csharp
using PatternKit.Generators.CacheStampedeProtection;

[GenerateCacheStampedeProtection(typeof(ProductAvailabilitySnapshot), FactoryMethodName = "CreateGenerated", PolicyName = "product-catalog-single-flight")]
public static partial class GeneratedProductCatalogStampedeProtectionPolicy;
```

Generated usage:

```csharp
var policy = GeneratedProductCatalogStampedeProtectionPolicy.CreateGenerated();
```

The host type must be partial. `FactoryMethodName` and `PolicyName` must be non-empty when provided.
