# Feature Toggle

Feature Toggle controls whether a capability is enabled for a request, tenant, user, or environment without changing the calling workflow. Use it for staged rollout, kill switches, tenant-specific capabilities, and operational experiments.

PatternKit provides `IFeatureToggleSet<TContext>` and `FeatureToggleSet<TContext>` in `PatternKit.Application.FeatureToggles`.

```csharp
var toggles = FeatureToggleSet<CheckoutFeatureContext>.Create("checkout-features")
    .AddRule("new-checkout", false, context => context.Tenant == "beta")
    .AddRule("fraud-review", false, context => context.Total >= 500m)
    .Build();

var enabled = toggles.IsEnabled(
    "new-checkout",
    new CheckoutFeatureContext("beta", "Gold", 125m));
```

Toggle decisions include the toggle name, enabled state, whether the toggle was configured, and an evaluation reason. Missing toggles evaluate to disabled so application code can fail closed.

Use the source-generated path when toggle names and targeting methods are stable. Register `IFeatureToggleSet<TContext>` as scoped when the evaluation context depends on request, tenant, or user services.

See also:

- [Feature Toggle generator](../../generators/feature-toggle.md)
- [Checkout Feature Toggle example](../../examples/checkout-feature-toggle-pattern.md)
