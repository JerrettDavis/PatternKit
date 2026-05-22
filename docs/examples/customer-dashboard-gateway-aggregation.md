# Customer Dashboard Gateway Aggregation

The customer dashboard gateway aggregation example composes profile, order, and recommendation clients into one API-facing dashboard response.

```csharp
services.AddCustomerDashboardGatewayAggregationDemo();

var runner = provider.GetRequiredService<CustomerDashboardGatewayAggregationDemoRunner>();
var dashboard = runner.RunGenerated("C-100");
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, and an ASP.NET Core minimal API mapping through `MapCustomerDashboardGatewayAggregation()`.
