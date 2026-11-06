# Lazy Load

Lazy Load defers expensive work until the value is actually needed. Use it for profile data, reference data, large aggregates, or remote calls that should not run during object construction or host startup.

The PatternKit runtime gives the deferred value an explicit name, single-flight loading, optional caching, invalidation, cancellation, and TTL-based refresh.

## Fluent Path

```csharp
var profile = LazyLoad<CustomerProfile>.Create("customer-profile")
    .LoadWith(ct => store.LoadAsync(customerId, ct))
    .WithTimeToLive(TimeSpan.FromMinutes(5))
    .Build();

var loaded = await profile.GetAsync(cancellationToken);
```

`GetAsync` returns a `LazyLoadResult<TValue>` so the caller can see the value, whether this call loaded it, whether it came from cache, and when the cached value was originally loaded. Repeated calls share the cached value until `Invalidate` is called or the TTL expires.

## DI Usage

Register lazy loaders as scoped or singleton services depending on ownership of the cached value:

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

Use scoped registration for request-owned data and singleton registration for process-wide reference data.

See [Customer Profile Lazy Load](../../examples/customer-profile-lazy-load.md).
