# Visitor — Event Processing (Orchestration)

Use action and async visitors to route domain events to handlers. Keep orchestration logic discoverable and easy to extend.

---

## Domain Events

```csharp
abstract record Event(DateTimeOffset At);
record OrderPlaced(string OrderId, decimal Total, DateTimeOffset At) : Event(At);
record PaymentCaptured(string OrderId, string TxId, DateTimeOffset At) : Event(At);
record ShipmentScheduled(string OrderId, string Tracking, DateTimeOffset At) : Event(At);
record AuditLog(string Message, DateTimeOffset At) : Event(At);
```

---

## Async Orchestrator

```csharp
public interface IEmail { Task SendAsync(string to, string subject, CancellationToken ct); }
public interface IAccounting { Task RecordAsync(string orderId, decimal total, CancellationToken ct); }
public interface IShipping { Task ScheduleAsync(string orderId, string tracking, CancellationToken ct); }

public static class EventOrchestrator
{
    public static AsyncActionVisitor<Event> Build(IEmail email, IAccounting acct, IShipping ship)
        => AsyncActionVisitor<Event>
            .Create()
            .On<OrderPlaced>(async (e, ct) =>
            {
                await acct.RecordAsync(e.OrderId, e.Total, ct);
                await email.SendAsync("ops@example.com", $"Order {e.OrderId} placed", ct);
            })
            .On<PaymentCaptured>(async (e, ct) =>
                await email.SendAsync("ops@example.com", $"Payment captured {e.TxId}", ct))
            .On<ShipmentScheduled>(async (e, ct) =>
                await ship.ScheduleAsync(e.OrderId, e.Tracking, ct))
            .Default((e, _) => { /* ignore or metric */ return default; })
            .Build();
}
```

Usage
```csharp
var orchestrator = EventOrchestrator.Build(email, accounting, shipping);
foreach (var e in events) await orchestrator.VisitAsync(e, ct);
```

---

## Synchronous Projection (Result Visitor)

```csharp
public static class EventProjection
{
    public static Visitor<Event, string> BuildSummary()
        => Visitor<Event, string>
            .Create()
            .On<OrderPlaced>(e => $"Placed:{e.OrderId}:{e.Total:C}")
            .On<PaymentCaptured>(e => $"Paid:{e.OrderId}:{e.TxId}")
            .On<ShipmentScheduled>(e => $"Ship:{e.OrderId}:{e.Tracking}")
            .Default(e => $"Audit:{e.At:O}")
            .Build();
}
```

---

## Testing Tips

- Cover each event type and the default path.
- Assert side effects by using test doubles or counters.
- Include cancellation tests to ensure `ct` is respected.

