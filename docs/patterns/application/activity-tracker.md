# Activity Tracker

`ActivityTracker` models tracker-based gating for active work. A caller acquires an `ActivityLease` when work begins, and releases it by disposing the lease or completing the activity id. Dependent state is based only on whether any activities exist.

```csharp
var tracker = ActivityTracker.Create("dashboard-loading").Build();

using var loadOrders = tracker.Track("orders", correlationId: "REQ-100");

if (tracker.IsBlocked)
{
    // Show the loading indicator or hold dependent work.
}
```

Use it for loading wheels, import gates, page readiness, background refresh coordination, and other workflows where work can enter or leave independently and the block state is `ActiveCount > 0`.

The source-generated path uses `[GenerateActivityTracker]` to produce a named tracker factory:

```csharp
[GenerateActivityTracker(FactoryMethodName = "CreateGenerated", TrackerName = "dashboard-loading")]
public static partial class GeneratedDashboardActivityTracker;
```

`DashboardActivityTrackerDemo` shows a production-oriented `IServiceCollection` registration that drives dashboard loading visibility from tracked widget loads. Import it with `AddDashboardActivityTrackerDemo()` or the aggregate `AddPatternKitExamples()` registration.
