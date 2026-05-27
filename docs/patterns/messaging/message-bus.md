# Message Bus

`MessageBus<TPayload>` provides a typed in-process topic bus over `MessageChannel<TPayload>` subscribers. Use it when an application needs one integration surface for publishing domain or integration events to multiple internal consumers without coupling publishers to channel instances.

```csharp
var bus = MessageBus<OrderEvent>.Create("order-bus")
    .Route("accepted", fulfillmentChannel)
    .Route("accepted", auditChannel)
    .Route("paid", billingChannel)
    .Build();

var result = bus.Publish("accepted", Message<OrderEvent>.Create(orderAccepted));
```

The fluent path supports runtime `Subscribe(topic, channel)` calls, so host bootstrapping can add tenant, module, or feature-specific routes from normal `IServiceCollection` registrations.

The source-generated path uses `[GenerateMessageBus]` and `[MessageBusRoute]` to create a strongly typed topology factory:

```csharp
[GenerateMessageBus(typeof(OrderEvent), BusName = "order-bus")]
public static partial class GeneratedOrderBus
{
    [MessageBusRoute("accepted")]
    private static MessageChannel<OrderEvent> Accepted()
        => MessageChannel<OrderEvent>.Create("accepted-orders").Build();
}
```

`OrderMessageBusExample` demonstrates fluent and generated publishing. Import it with `AddOrderMessageBusDemo()` or the aggregate `AddPatternKitExamples()` registration.
