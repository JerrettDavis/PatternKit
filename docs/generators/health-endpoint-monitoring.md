# Health Endpoint Monitoring Generator

`[GenerateHealthEndpoint]` creates a typed `HealthEndpoint<TContext>` factory from static check methods.

```csharp
[GenerateHealthEndpoint(typeof(FulfillmentHealthSnapshot), FactoryMethodName = "Create", EndpointName = "fulfillment-health")]
public static partial class FulfillmentHealthEndpoint
{
    [HealthEndpointCheck("database", Order = 1)]
    private static HealthEndpointCheckResult CheckDatabase(FulfillmentHealthSnapshot snapshot)
        => snapshot.DatabaseOnline
            ? HealthEndpointCheckResult.HealthyCheck("database")
            : HealthEndpointCheckResult.UnhealthyCheck("database", "offline");
}
```

The generated factory is parameterless, so applications can register it in `IServiceCollection` and inject the endpoint into hosted services, readiness checks, or ASP.NET Core route handlers.

Diagnostics:

- `PKHEM001`: host type must be partial.
- `PKHEM002`: at least one health check is required.
- `PKHEM003`: health check signature is invalid.
