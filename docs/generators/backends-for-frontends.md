# Backends for Frontends Generator

`[GenerateBackendsForFrontends]` creates a typed `BackendsForFrontends<TRequest, TResponse>` factory from selector and handler method pairs.

```csharp
[GenerateBackendsForFrontends(typeof(CommerceClientRequest), typeof(CommerceClientResponse), GatewayName = "commerce-bff")]
public static partial class CommerceBff
{
    [FrontendSelector("mobile")]
    private static bool IsMobile(CommerceClientRequest request) => request.Client == "mobile";

    [FrontendHandler("mobile")]
    private static CommerceClientResponse Mobile(BackendsForFrontendsContext<CommerceClientRequest> ctx)
        => new(ctx.Request.CustomerId, "compact", 2, false);
}
```

Diagnostics:

- `PKBFF001`: host type must be partial.
- `PKBFF002`: selectors and handlers must be paired by frontend name.
- `PKBFF003`: selector, handler, or fallback signature is invalid.
- `PKBFF004`: frontend names must be unique.
