# Customer Profile Lazy Load

The customer profile lazy-load example shows a production-shaped deferred profile lookup. It includes a fluent lazy loader, a source-generated lazy loader factory, and an `IServiceCollection` extension that registers the loader and application service.

Import it into a host:

```csharp
services.AddCustomerProfileLazyLoadDemo();
```

For reusable app-level registration without importing examples:

```csharp
services.AddPatternKitLazyLoad<CustomerProfile>(
    (provider, ct) =>
    {
        var store = provider.GetRequiredService<ICustomerProfileStore>();
        return store.LoadAsync(customerId, ct);
    },
    "customer-profile",
    builder => builder.WithTimeToLive(TimeSpan.FromMinutes(5)));
```

The example validates that the expensive load is deferred, cached, refreshable through invalidation, and importable through standard Microsoft dependency injection.
