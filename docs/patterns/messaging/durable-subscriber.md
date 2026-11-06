# Durable Subscriber

`DurableSubscriber<TPayload>` replays messages from a `MessageStore<TPayload>` for a named subscriber and records a checkpoint only after the configured handlers succeed.

Use it when an application service needs to rebuild or resume a projection from an audit/replay store without reprocessing messages that were already acknowledged.

```csharp
var subscriber = DurableSubscriber<OrderShipmentEvent>.Create("shipment-projection")
    .From(messageStore)
    .TrackWith(checkpointStore)
    .Handle("project", (stored, context) =>
    {
        projection.Apply(stored.Message.Payload);
        return DurableSubscriberHandlerResult.Success("project");
    })
    .Build();

var result = subscriber.CatchUp();
```

## IoC Integration

Register the message store, checkpoint store, projection, subscriber, and application service with `IServiceCollection`.

```csharp
services.AddSingleton(_ => OrderDurableSubscribers.CreateStore());
services.AddSingleton<IDurableSubscriberCheckpointStore, InMemoryDurableSubscriberCheckpointStore>();
services.AddSingleton<OrderShipmentProjection>();
services.AddSingleton(sp => OrderDurableSubscribers.Create(
    sp.GetRequiredService<MessageStore<OrderShipmentEvent>>(),
    sp.GetRequiredService<IDurableSubscriberCheckpointStore>(),
    sp.GetRequiredService<OrderShipmentProjection>()));
services.AddSingleton<OrderDurableSubscriberService>();
```

See `src/PatternKit.Examples/Messaging/OrderDurableSubscriberExample.cs` for the production-shaped example.
