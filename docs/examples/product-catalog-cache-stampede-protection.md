# Product Catalog Cache Stampede Protection

This example protects product availability reads from duplicate origin loads when two requests miss the same catalog key at the same time.

```csharp
var request = new ProductAvailabilityRequest("SKU-100", "us");
var results = await ProductCatalogStampedeProtectionDemoRunner.RunFluentAsync(request);
```

The generated route uses the same workflow through a generated policy factory:

```csharp
var policy = GeneratedProductCatalogStampedeProtectionPolicy.CreateGenerated();
var service = new ProductCatalogStampedeProtectionService(policy, origin);
```

Import the demo into a host with:

```csharp
services.AddProductCatalogStampedeProtectionDemo();
```

The registration provides `CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot>`, `ProductCatalogOrigin`, `ProductCatalogStampedeProtectionService`, and `ProductCatalogStampedeProtectionDemoRunner`.
