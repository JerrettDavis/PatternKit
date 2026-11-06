# Dynamic Router Generator

`[GenerateDynamicRouter]` creates a typed `DynamicRouter<TPayload, TResult>` factory from static predicate and handler methods.

```csharp
[GenerateDynamicRouter(typeof(FulfillmentOrder), typeof(FulfillmentRouteDecision))]
public static partial class GeneratedOrderDynamicRouter
{
    private static bool IsVip(Message<FulfillmentOrder> message, MessageContext context)
        => message.Payload.Total >= 1_000m;

    [DynamicRoute("vip", 1, nameof(IsVip))]
    private static FulfillmentRouteDecision Vip(Message<FulfillmentOrder> message, MessageContext context)
        => new("vip", "white-glove");

    [DynamicRouteDefault]
    private static FulfillmentRouteDecision Default(Message<FulfillmentOrder> message, MessageContext context)
        => new("default", "standard");
}
```

The generated factory returns a normal `DynamicRouter<TPayload, TResult>`, so applications can still register or unregister routes at runtime after startup.

Diagnostics:

- `PKDR001`: the target type must be partial.
- `PKDR002`: at least one `[DynamicRoute]` method is required.
- `PKDR003`: route handlers and predicates must be static and use `Message<TPayload>` plus `MessageContext`.
- `PKDR004`: the default handler must match the generated result signature.
- `PKDR005`: route names and order values must be unique in the generated initial table.
