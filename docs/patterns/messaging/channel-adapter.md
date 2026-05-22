# Channel Adapter

Channel Adapter bridges an external transport shape to PatternKit message channels.

```csharp
var adapter = ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage>
    .Create("erp-orders-adapter")
    .ReceiveInto(inboundChannel)
    .SendFrom(outboundChannel)
    .MapInbound((document, context) => Message<OrderIntegrationMessage>.Create(command))
    .MapOutbound((message, context) => ToErpDocument(message.Payload))
    .Build();
```

Use it at application boundaries where a broker callback, HTTP webhook, file reader, or partner SDK exposes a DTO that should be translated into internal messages. The outbound path translates internal messages back into the transport DTO without making PatternKit own the external infrastructure.

The source-generated path uses `[GenerateChannelAdapter]`, `[ChannelAdapterInbound]`, and `[ChannelAdapterOutbound]`. Import the ERP example through `AddErpChannelAdapterDemo()` or `AddPatternKitExamples()`.
