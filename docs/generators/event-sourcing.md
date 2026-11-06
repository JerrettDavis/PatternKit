# Event Sourcing Generator

`GenerateEventStoreAttribute` creates a typed `InMemoryEventStore<TEvent,TStreamId>` factory for an event stream.

```csharp
[GenerateEventStore(typeof(OrderEvent), typeof(string), FactoryName = "CreateStore", StoreName = "order-events")]
public static partial class GeneratedOrderEventStore;
```

The generated factory is equivalent to:

```csharp
InMemoryEventStore<OrderEvent, string>
    .Create("order-events")
    .Build();
```

Diagnostics:

- `PKES001`: host type must be partial.
