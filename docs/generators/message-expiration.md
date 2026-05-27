# Message Expiration Generator

`[GenerateMessageExpiration]` emits a typed factory for `MessageExpiration<TPayload>`.

```csharp
[GenerateMessageExpiration(
    typeof(OrderCommand),
    FactoryName = "Create",
    PolicyName = "order-message-expiration",
    HeaderName = "x-order-expires-at",
    DefaultTtlMilliseconds = 1200000,
    ExpiredReason = "Order command expired before fulfillment accepted it.")]
public static partial class GeneratedOrderMessageExpiration;
```

The generated route is useful when expiration metadata is part of a stable messaging contract. It keeps policy names, header names, TTLs, and rejection reasons declared once and benchmarkable.

## Options

| Option | Default | Purpose |
| --- | --- | --- |
| `FactoryName` | `Create` | Generated factory method name. |
| `PolicyName` | `message-expiration` | Name returned in evaluation results. |
| `HeaderName` | `expires-at` | Deadline header. |
| `DefaultTtlMilliseconds` | `0` | Positive values configure the default `Stamp` TTL. |
| `PreserveExisting` | `true` | Keeps existing deadlines when stamping. |
| `ExpiredReason` | `Message expired before processing.` | Reason returned for expired messages. |
