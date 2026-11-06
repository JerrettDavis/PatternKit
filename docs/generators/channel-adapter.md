# Channel Adapter Generator

`[GenerateChannelAdapter]` creates a typed `ChannelAdapter<TExternal, TPayload>` factory.

```csharp
[GenerateChannelAdapter(typeof(ErpOrderDocument), typeof(OrderIntegrationMessage), FactoryName = "Create", AdapterName = "erp-orders-adapter")]
public static partial class ErpOrdersAdapter
{
    [ChannelAdapterInbound]
    private static Message<OrderIntegrationMessage> ToMessage(ErpOrderDocument document, MessageContext context)
        => Message<OrderIntegrationMessage>.Create(new(document.ExternalOrderId, decimal.Parse(document.Total)));

    [ChannelAdapterOutbound]
    private static ErpOrderDocument ToExternal(Message<OrderIntegrationMessage> message, MessageContext context)
        => new(message.Payload.OrderId, message.Payload.Total.ToString("0.00"));
}
```

The generated factory accepts inbound and outbound `MessageChannel<TPayload>` instances so the adapter can be composed through `IServiceCollection`.

Diagnostics:

- `PKCAD001`: host type must be partial.
- `PKCAD002`: exactly one inbound translator is required.
- `PKCAD003`: exactly one outbound translator is required.
- `PKCAD004`: inbound translator signature is invalid.
- `PKCAD005`: outbound translator signature is invalid.
