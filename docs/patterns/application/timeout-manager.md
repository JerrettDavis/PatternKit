# Timeout Manager

Timeout Manager tracks deadlines for workflow work that should expire unless it is completed or canceled first. Use it when a saga, reservation, approval, shipment hold, or background operation needs a clear time boundary without coupling the domain workflow to a specific scheduler.

`TimeoutManager<TKey>` provides the fluent runtime path:

```csharp
var manager = TimeoutManager<Guid>
    .Create("order-reservation-timeouts")
    .Build();

manager.ScheduleAfter(orderId, TimeSpan.FromMinutes(15), requestId);
var expired = manager.ExpireDue(DateTimeOffset.UtcNow.AddMinutes(20));
```

The manager is in-memory and intentionally small. Application code can persist the returned `TimeoutRecord<TKey>` values in its own store or use the manager inside a hosted service that loads pending deadlines, calls `ExpireDue`, and dispatches expiration work through existing PatternKit messaging or workflow patterns.

## Use When

- Work can enter and leave a pending state at arbitrary times.
- The only decision needed by downstream code is whether deadlines are still pending or due.
- Expiration should be explicit and testable instead of hidden in timers.

## Compare With

- Use Saga / Process Manager when the workflow owns multiple correlated messages and state transitions.
- Use Scheduler Agent Supervisor when the system owns recurring scheduled work.
- Use Activity Tracker when the concern is active/inactive gating rather than time-based expiration.
