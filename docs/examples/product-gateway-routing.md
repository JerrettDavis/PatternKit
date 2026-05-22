# Product Gateway Routing

The product gateway routing example routes inventory and pricing requests to separate downstream APIs while unknown paths use a fallback response.

```csharp
services.AddProductGatewayRoutingDemo();

var runner = provider.GetRequiredService<ProductGatewayRoutingDemoRunner>();
var result = runner.RunGenerated(new ProductGatewayRequest("/inventory/SKU-100", "tenant-a"));
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, and an ASP.NET Core minimal API mapping through `MapProductGatewayRouting()`.
