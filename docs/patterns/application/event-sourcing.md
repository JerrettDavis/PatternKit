# Event Sourcing

Event Sourcing stores domain changes as an append-only stream of facts and rebuilds state by replaying those facts. Use it when auditability, temporal history, integration handoff, or projection rebuilds are first-class requirements.

PatternKit provides `IEventStore<TEvent,TStreamId>` and `InMemoryEventStore<TEvent,TStreamId>` in `PatternKit.Application.EventSourcing`.

```csharp
var store = InMemoryEventStore<OrderEvent, string>
    .Create("order-events")
    .Build();

await store.AppendAsync("order-100", 0, [
    new OrderPlaced("order-100", "customer-1", 125m, DateTimeOffset.UtcNow),
    new OrderPaid("order-100", "payment-1", DateTimeOffset.UtcNow)
]);

var stream = await store.ReadStreamAsync("order-100");
var summary = OrderProjection.Project(store.Name, stream);
```

Appends require an expected version. A stale expected version returns `EventStoreAppendStatus.Conflict` and does not mutate the stream, giving callers an optimistic concurrency boundary.

Use the source-generated path when the event base type and stream identity type are stable. Register `IEventStore<TEvent,TStreamId>` as scoped when the store is backed by request-scoped database sessions, tenant boundaries, or unit-of-work infrastructure.

See also:

- [Event Sourcing generator](../../generators/event-sourcing.md)
- [Order Event Sourcing example](../../examples/order-event-sourcing-pattern.md)
