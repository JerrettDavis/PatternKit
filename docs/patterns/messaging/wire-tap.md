# Wire Tap

Use `WireTap<TPayload>` when production message flows need audit, metrics, or diagnostics side channels without changing the original message. Each tap is named, invoked in order, and reported in the returned `WireTapResult<TPayload>`.

```csharp
var tap = WireTap<OrderEvent>.Create("order-observability")
    .AddTap("audit", (message, context) => audit.Record(message.Payload, context))
    .AddTap("metrics", (message, _) => metrics.Record(message.Payload))
    .Build();
```

The generated path uses `[GenerateWireTap]` on a partial type and `[WireTapHandler]` on static handler methods. Import the production example through `AddOrderWireTapDemo()` or the aggregate `AddPatternKitExamples()` registration.
