# Fulfillment Health Endpoint Monitoring

The fulfillment health endpoint example evaluates database reachability, message broker connectivity, and fulfillment queue depth.

```csharp
services.AddFulfillmentHealthEndpointDemo();

var service = provider.GetRequiredService<FulfillmentHealthEndpointService>();
var report = service.Evaluate();
```

ASP.NET Core applications can expose the same endpoint through the route builder extension:

```csharp
app.MapFulfillmentHealthEndpoint("/health/fulfillment");
```

The example includes fluent and source-generated construction, `IServiceCollection` registration, Generic Host-friendly service composition, and ASP.NET Core minimal API integration.
