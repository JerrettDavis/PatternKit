# Gateway Aggregation Generator

`[GenerateGatewayAggregation]` creates a typed `GatewayAggregation<TRequest, TResponse>` factory from downstream fetch methods and one composer.

```csharp
[GenerateGatewayAggregation(typeof(CustomerDashboardRequest), typeof(CustomerDashboardResponse), GatewayName = "customer-dashboard")]
public static partial class CustomerDashboardGateway
{
    [GatewayAggregationFetch("profile")]
    private static CustomerProfile Profile(CustomerDashboardRequest request) => new(request.CustomerId, "Ada");

    [GatewayAggregationComposer]
    private static CustomerDashboardResponse Compose(GatewayAggregationContext<CustomerDashboardRequest> ctx)
        => new(ctx.Require<CustomerProfile>("profile").CustomerId, 0);
}
```

Diagnostics:

- `PKGA001`: host type must be partial.
- `PKGA002`: at least one fetch and exactly one composer are required.
- `PKGA003`: fetch or composer signature is invalid.
- `PKGA004`: fetch names must be unique.
