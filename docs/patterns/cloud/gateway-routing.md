# Gateway Routing

Gateway Routing dispatches one inbound request to one downstream handler based on ordered route predicates.

```csharp
var router = GatewayRouting<ProductGatewayRequest, ProductGatewayResponse>
    .Create("product-gateway-routing")
    .Route("inventory", request => request.Path.StartsWith("/inventory/"), inventory.Get)
    .Route("pricing", request => request.Path.StartsWith("/pricing/"), pricing.Get)
    .Fallback("not-found", request => new("fallback", $"not-found:{request.Path}"))
    .Build();

var result = router.Route(request);
```

Use it in API gateways, BFFs, endpoint facades, and service ingress points where request shape determines the downstream owner. The runtime result records the selected route and whether fallback handling was used.

The source-generated path uses `[GenerateGatewayRouting]`, `[GatewayRoute]`, `[GatewayRouteHandler]`, and `[GatewayRouteFallback]`. Import the example through `AddProductGatewayRoutingDemo()` or `AddPatternKitExamples()`.
