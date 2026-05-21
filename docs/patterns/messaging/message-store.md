# Message Store

Message Store persists message envelopes so support, replay, and operational audit workflows can inspect what moved through a pipeline.

`MessageStore<TPayload>` provides a fluent runtime path:

```csharp
var store = MessageStore<OrderSubmitted>.Create("order-audit")
    .IdentifyBy((message, context) => message.Headers.MessageId!)
    .RetainWhen(stored => !stored.Message.Payload.ContainsSensitiveData)
    .Build();

var result = store.Append(message.WithMessageId("msg-100").WithCorrelationId("checkout-100"));
var replay = store.Replay(MessageStoreQuery.ForCorrelation("checkout-100"));
```

Use it when an app needs durable lookup semantics around messages without mixing audit/replay concerns into message handlers. In production, the same shape can sit behind a hosted service, ASP.NET Core endpoint, queue worker, or support tool through `IServiceCollection`.

The source-generated path uses `[GenerateMessageStore]`, `[MessageStoreIdentity]`, and `[MessageStoreRetention]` to create the same fluent store factory from annotated static hooks.
