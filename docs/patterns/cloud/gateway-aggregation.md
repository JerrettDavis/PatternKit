# Gateway Aggregation

Gateway Aggregation composes several downstream calls behind one API-facing operation.

```csharp
var gateway = GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse>
    .Create("customer-dashboard")
    .Fetch<CustomerProfile>("profile", profiles.GetProfile)
    .Fetch<CustomerOrderSummary>("orders", orders.GetOrders)
    .Compose(ctx => new(
        ctx.Require<CustomerProfile>("profile").CustomerId,
        ctx.Require<CustomerOrderSummary>("orders").OpenOrders))
    .Build();

var result = gateway.Aggregate(request);
```

Use it at an API gateway, BFF, or application facade boundary when the caller needs one response but the data lives behind multiple internal services. The runtime path captures downstream failures per part and returns an explicit aggregate failure when the response cannot be composed.

The source-generated path uses `[GenerateGatewayAggregation]`, `[GatewayAggregationFetch]`, and `[GatewayAggregationComposer]`. Import the example through `AddCustomerDashboardGatewayAggregationDemo()` or `AddPatternKitExamples()`.
