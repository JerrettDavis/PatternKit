# Backends for Frontends

Backends for Frontends creates client-specific API facades over shared backend capabilities.

```csharp
var bff = BackendsForFrontends<CommerceClientRequest, CommerceClientResponse>
    .Create("commerce-bff")
    .Frontend("mobile", request => request.Client == "mobile", ctx => new(ctx.Request.CustomerId, "compact", 2, false))
    .Frontend("web", request => request.Client == "web", ctx => new(ctx.Request.CustomerId, "rich", 2, true))
    .Fallback(ctx => new(ctx.Request.CustomerId, "standard", 2, true))
    .Build();

var result = bff.Dispatch(request);
```

Use it when mobile, web, partner, or internal clients need different response shapes or orchestration paths while the same backend services remain shared. The runtime path returns an explicit result with the selected frontend name and captures handler failures without forcing exception-based control flow.

The source-generated path uses `[GenerateBackendsForFrontends]`, `[FrontendSelector]`, `[FrontendHandler]`, and `[FrontendFallback]`. Import the example through `AddCommerceBackendsForFrontendsDemo()` or `AddPatternKitExamples()`.
