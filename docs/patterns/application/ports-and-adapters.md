# Ports and Adapters

Ports and Adapters keeps application use cases independent from delivery mechanisms and infrastructure adapters.

```csharp
var pipeline = PortsAndAdaptersPipeline<OrderEntryHttpRequest, PlaceOrderCommand, PlaceOrderResult, OrderEntryHttpResponse>
    .Create("order-entry")
    .AdaptInboundWith(request => new PlaceOrderCommand(request.OrderId, request.CustomerEmail, request.Total))
    .HandleWith((command, ct) => applicationPort.PlaceOrderAsync(command, ct))
    .AdaptOutboundWith(result => new OrderEntryHttpResponse(result.Accepted ? 202 : 409, result.OrderId, result.Message))
    .Build();
```

Use the fluent path when adapters are assembled at runtime from `IServiceCollection`. Use the generator path when the adapter methods are stable and you want a named factory with minimal ceremony.
