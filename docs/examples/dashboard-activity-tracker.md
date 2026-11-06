# Dashboard Activity Tracker Example

The dashboard activity tracker example shows a loading-indicator gate composed through standard dependency injection:

- fluent `DashboardActivityTrackers.CreateFluent()` construction;
- source-generated `GeneratedDashboardActivityTracker.CreateGenerated()` construction;
- `AddDashboardActivityTrackerDemo()` for `IServiceCollection` integration;
- TinyBDD coverage for fluent behavior, generated parity, direct DI registration, and aggregate `AddPatternKitExamples()` import.

```csharp
var services = new ServiceCollection();
services.AddDashboardActivityTrackerDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var runner = provider.GetRequiredService<DashboardActivityTrackerDemoRunner>();
var summary = runner.RunGenerated(new DashboardLoadRequest("REQ-100", ["orders", "inventory"]));
```

The summary reports whether the loading indicator should be visible, how many widget loads are active, and which widgets currently block dependent UI state.
