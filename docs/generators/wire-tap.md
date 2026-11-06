# Wire Tap Generator

`[GenerateWireTap]` creates a typed `WireTap<TPayload>` factory from ordered static tap handlers.

```csharp
[GenerateWireTap(typeof(OrderEvent), FactoryName = "Create", TapName = "order-observability")]
public static partial class GeneratedOrderWireTap
{
    [WireTapHandler("audit", 10)]
    private static void Audit(Message<OrderEvent> message, MessageContext context) { }
}
```

Handlers must be static `void` methods with `(Message<TPayload>, MessageContext)` parameters. Duplicate handler names or orders are reported at compile time.
