# Checkout Feature Toggle Pattern

This production-shaped example shows checkout rollout rules evaluated through a typed toggle set.

It demonstrates:

- fluent `FeatureToggleSet<CheckoutFeatureContext>` construction
- generated toggle set factory with `[GenerateFeatureToggleSet]`
- contextual rollout, fraud-review, and loyalty-offer rules
- scoped `IFeatureToggleSet<CheckoutFeatureContext>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddCheckoutFeatureToggleDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<CheckoutFeatureToggleWorkflow>();
var summary = workflow.Evaluate("beta", "Gold", 650m);
```

The registered toggle set is scoped so importing applications can compose it with request-scoped tenant, user, environment, or experiment services.

Files:

- `src/PatternKit.Examples/FeatureToggleDemo/CheckoutFeatureToggleDemo.cs`
- `test/PatternKit.Examples.Tests/FeatureToggleDemo/CheckoutFeatureToggleDemoTests.cs`
