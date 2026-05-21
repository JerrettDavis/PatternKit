# Message Filter

Use `MessageFilter<TPayload>` when a consumer should only receive messages that satisfy explicit allow rules. The fluent API keeps filtering logic named and testable, while the result exposes the matched rule or rejection reason for observability.

```csharp
var filter = MessageFilter<OrderCommand>.Create("order-fraud-screen")
    .AllowWhen("trusted-customer", (message, _) => message.Payload.CustomerTier == "trusted")
    .AllowWhen("verified-low-value", (message, _) => message.Payload.Total <= 100m)
    .RejectUnmatched("Order requires fraud review before fulfillment.")
    .Build();
```

For generated factories, annotate a partial type with `[GenerateMessageFilter]` and mark static predicates with `[MessageFilterRule]`. Import production examples through `AddOrderMessageFilterDemo()` or the aggregate `AddPatternKitExamples()` registration.
