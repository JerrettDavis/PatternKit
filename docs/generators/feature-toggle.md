# Feature Toggle Generator

`GenerateFeatureToggleSetAttribute` creates a typed `FeatureToggleSet<TContext>` factory from attributed rule methods.

```csharp
[GenerateFeatureToggleSet(typeof(CheckoutFeatureContext), FactoryName = "CreateToggles", SetName = "checkout-features")]
public static partial class GeneratedCheckoutFeatureToggles
{
    [FeatureToggleRule("new-checkout")]
    private static bool IsBetaTenant(CheckoutFeatureContext context) => context.Tenant == "beta";
}
```

The generated factory is equivalent to:

```csharp
FeatureToggleSet<CheckoutFeatureContext>
    .Create("checkout-features")
    .AddRule("new-checkout", false, IsBetaTenant)
    .Build();
```

Diagnostics:

- `PKFT001`: host type must be partial.
- `PKFT002`: at least one `[FeatureToggleRule]` method is required.
- `PKFT003`: rule methods must be static and return `bool` from one context parameter.
