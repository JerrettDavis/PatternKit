# Domain Event Generator

`GenerateDomainEventDispatcherAttribute` creates a typed `DomainEventDispatcher<TEventBase>` factory from attributed handler methods.

```csharp
[GenerateDomainEventDispatcher(typeof(OrderDomainEvent), FactoryName = "CreateDispatcher", DispatcherName = "order-domain-events")]
public static partial class GeneratedOrderDomainEvents
{
    [DomainEventHandler(typeof(OrderPlaced), 10)]
    private static ValueTask Project(OrderPlaced domainEvent, CancellationToken cancellationToken)
    {
        projection.Apply(domainEvent);
        return ValueTask.CompletedTask;
    }
}
```

The generated factory is equivalent to:

```csharp
DomainEventDispatcher<OrderDomainEvent>
    .Create("order-domain-events")
    .Handle<OrderPlaced>(Project)
    .Build();
```

Handlers are grouped by event type and ordered by the `order` argument on `[DomainEventHandler]`.

Diagnostics:

- `PKDE001`: host type must be partial.
- `PKDE002`: at least one `[DomainEventHandler]` method is required.
- `PKDE003`: handler must be static and return `ValueTask` from `(TEvent, CancellationToken)`, and the event type must derive from the dispatcher base event type.
- `PKDE004`: handler order values must be unique per event type.
