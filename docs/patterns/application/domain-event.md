# Domain Event

Domain Event models facts that already happened inside a domain or application workflow. Use it to decouple aggregate decisions from projections, audit trails, notifications, and integration handoff logic.

PatternKit provides `IDomainEvent`, `IDomainEventDispatcher<TEventBase>`, and `DomainEventDispatcher<TEventBase>` in `PatternKit.Application.DomainEvents`.

```csharp
var dispatcher = DomainEventDispatcher<OrderDomainEvent>
    .Create("order-domain-events")
    .Handle<OrderPlaced>((domainEvent, ct) =>
    {
        projection.Apply(domainEvent);
        return ValueTask.CompletedTask;
    })
    .Handle<OrderPlaced>((domainEvent, ct) =>
    {
        audit.Add($"placed:{domainEvent.OrderId}");
        return ValueTask.CompletedTask;
    })
    .Build();

var result = await dispatcher.DispatchAsync(new OrderPlaced(id, now, "order-100", "customer-1", 125m));
```

The dispatcher returns `DomainEventDispatchResult` so callers can distinguish handled, unhandled, and failed dispatch. Register the dispatcher as scoped when handlers depend on scoped projections, unit-of-work state, tenant context, or request services.

Use the source-generated path when event handlers are stable application code and you want compiler diagnostics for missing partial hosts, invalid handler signatures, or duplicated handler order.

See also:

- [Domain Event generator](../../generators/domain-event.md)
- [Order Domain Event example](../../examples/order-domain-event-pattern.md)
