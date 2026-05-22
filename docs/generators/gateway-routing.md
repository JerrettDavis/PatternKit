# Gateway Routing Generator

`[GenerateGatewayRouting]` creates a typed `GatewayRouting<TRequest, TResponse>` factory from route predicates, matching route handlers, and one fallback handler.

```csharp
[GenerateGatewayRouting(typeof(ProductGatewayRequest), typeof(ProductGatewayResponse), GatewayName = "product-gateway-routing")]
public static partial class ProductGateway
{
    [GatewayRoute("inventory")]
    private static bool IsInventory(ProductGatewayRequest request) => request.Path.StartsWith("/inventory/");

    [GatewayRouteHandler("inventory")]
    private static ProductGatewayResponse Inventory(ProductGatewayRequest request) => new("inventory", request.Path);

    [GatewayRouteFallback("not-found")]
    private static ProductGatewayResponse NotFound(ProductGatewayRequest request) => new("fallback", request.Path);
}
```

Diagnostics:

- `PKGR001`: host type must be partial.
- `PKGR002`: at least one route, matching route handlers, and exactly one fallback are required.
- `PKGR003`: route or handler signature is invalid.
- `PKGR004`: route names must be unique.
- `PKGR005`: each route predicate must have exactly one matching handler.
