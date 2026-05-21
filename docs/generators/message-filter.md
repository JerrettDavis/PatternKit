# Message Filter Generator

`[GenerateMessageFilter]` creates a typed `MessageFilter<TPayload>` factory from ordered static predicate methods.

```csharp
[GenerateMessageFilter(typeof(OrderCommand), FactoryName = "Create", FilterName = "order-fraud-screen")]
public static partial class GeneratedOrderFilter
{
    [MessageFilterRule("trusted-customer", 10)]
    private static bool IsTrusted(Message<OrderCommand> message, MessageContext context)
        => message.Payload.CustomerTier == "trusted";
}
```

Rules must be static methods returning `bool` with `(Message<TPayload>, MessageContext)` parameters. Duplicate rule names or orders are reported at compile time.
