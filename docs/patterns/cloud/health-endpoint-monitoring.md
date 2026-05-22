# Health Endpoint Monitoring

Health Endpoint Monitoring exposes a typed status endpoint that evaluates application dependencies and returns a deterministic health report.

```csharp
var endpoint = HealthEndpoint<FulfillmentHealthSnapshot>
    .Create("fulfillment-health")
    .WithCheck("database", snapshot => snapshot.DatabaseOnline
        ? HealthEndpointCheckResult.HealthyCheck("database")
        : HealthEndpointCheckResult.UnhealthyCheck("database", "offline"))
    .WithCheck("queue-depth", snapshot => snapshot.QueueDepth <= 100
        ? HealthEndpointCheckResult.HealthyCheck("queue-depth")
        : HealthEndpointCheckResult.UnhealthyCheck("queue-depth", "backlog above target"))
    .Build();

var report = endpoint.Evaluate(snapshot);
```

Use it when service health needs to be composed from multiple business and infrastructure checks before it is exposed to load balancers, orchestrators, Generic Host startup validation, or ASP.NET Core endpoints.

The source-generated path uses `[GenerateHealthEndpoint]` and `[HealthEndpointCheck]`. Import the fulfillment example through `AddFulfillmentHealthEndpointDemo()`, map it through `MapFulfillmentHealthEndpoint()`, or include it in `AddPatternKitExamples()`.
