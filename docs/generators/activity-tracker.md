# Activity Tracker Generator

`[GenerateActivityTracker]` emits a named `ActivityTracker` factory for applications that want tracker gates declared at compile time.

```csharp
[GenerateActivityTracker(FactoryMethodName = "CreateGenerated", TrackerName = "dashboard-loading")]
public static partial class GeneratedDashboardActivityTracker;
```

The generated factory returns the normal runtime tracker:

```csharp
var tracker = GeneratedDashboardActivityTracker.CreateGenerated();
using var lease = tracker.Track("inventory", "REQ-100");
```

Diagnostics:

| ID | Meaning |
| --- | --- |
| `PKAT001` | The host type must be partial. |
| `PKAT002` | `FactoryMethodName` and `TrackerName` must be non-empty. |
