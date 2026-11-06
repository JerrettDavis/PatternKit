# Rate Limiting Generator

The rate limiting generator creates a strongly typed `RateLimitPolicy<TResult>` factory from declarative attributes. It is useful when tenant or route budgets should be visible on a policy host while still producing the same runtime policy as the fluent API.

```csharp
[GenerateRateLimitPolicy(
    typeof(SearchResponse),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "product-search",
    PermitLimit = 2,
    WindowMilliseconds = 60000)]
public static partial class ProductSearchRateLimitPolicy;
```

The generated factory returns `PatternKit.Cloud.RateLimiting.RateLimitPolicy<SearchResponse>` and configures the fixed-window permit budget.

## Rules

- The host type must be `partial`.
- `PermitLimit` must be at least `1`.
- `WindowMilliseconds` must be greater than `0`.
- The generator supports class and struct hosts, including static partial classes.

Diagnostics use the `PKRLT` prefix.
