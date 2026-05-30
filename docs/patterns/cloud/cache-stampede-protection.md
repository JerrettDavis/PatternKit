# Cache Stampede Protection

Cache Stampede Protection coordinates concurrent cache misses so only one origin load runs for a key while followers await the same result. Use it around expensive catalog, configuration, entitlement, pricing, or profile loads that can receive bursts of identical requests after expiration.

`CacheStampedeProtectionPolicy<TResult>` provides the fluent path:

```csharp
var policy = CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot>
    .Create("product-catalog-single-flight")
    .Build();

var result = await policy.GetOrLoadAsync(
    "us:SKU-100",
    ct => origin.LoadAsync(request, ct),
    cancellationToken);
```

The first caller owns the load. Concurrent callers for the same key receive `SharedFlight = true` and the same loaded value once the origin call completes.

Use the source-generated path for reusable policy factories:

```csharp
[GenerateCacheStampedeProtection(typeof(ProductAvailabilitySnapshot), FactoryMethodName = "CreateGenerated", PolicyName = "product-catalog-single-flight")]
public static partial class GeneratedProductCatalogStampedeProtectionPolicy;
```
