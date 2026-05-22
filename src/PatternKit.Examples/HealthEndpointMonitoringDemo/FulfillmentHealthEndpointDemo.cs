using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.HealthEndpointMonitoring;
using PatternKit.Generators.HealthEndpointMonitoring;

namespace PatternKit.Examples.HealthEndpointMonitoringDemo;

public sealed record FulfillmentHealthSnapshot(bool DatabaseOnline, bool BrokerOnline, int QueueDepth);

public sealed record FulfillmentHealthSummary(string EndpointName, bool Healthy, int PassedCount, int FailedCount);

public interface IFulfillmentHealthSnapshotProvider
{
    FulfillmentHealthSnapshot GetSnapshot();
}

public sealed class StaticFulfillmentHealthSnapshotProvider(FulfillmentHealthSnapshot snapshot) : IFulfillmentHealthSnapshotProvider
{
    public FulfillmentHealthSnapshot GetSnapshot() => snapshot;
}

public sealed class FulfillmentHealthEndpointService(
    HealthEndpoint<FulfillmentHealthSnapshot> endpoint,
    IFulfillmentHealthSnapshotProvider snapshots)
{
    public HealthEndpointReport Evaluate() => endpoint.Evaluate(snapshots.GetSnapshot());

    public FulfillmentHealthSummary Summarize()
    {
        var report = Evaluate();
        return new(report.EndpointName, report.Healthy, report.PassedCount, report.FailedCount);
    }
}

public static class FulfillmentHealthEndpoints
{
    public static HealthEndpoint<FulfillmentHealthSnapshot> CreateFluent()
        => HealthEndpoint<FulfillmentHealthSnapshot>.Create("fulfillment-health")
            .WithCheck("database", CheckDatabase)
            .WithCheck("message-broker", CheckBroker)
            .WithCheck("queue-depth", CheckQueueDepth)
            .Build();

    public static HealthEndpointCheckResult CheckDatabase(FulfillmentHealthSnapshot snapshot)
        => snapshot.DatabaseOnline
            ? HealthEndpointCheckResult.HealthyCheck("database", "database reachable")
            : HealthEndpointCheckResult.UnhealthyCheck("database", "database offline");

    public static HealthEndpointCheckResult CheckBroker(FulfillmentHealthSnapshot snapshot)
        => snapshot.BrokerOnline
            ? HealthEndpointCheckResult.HealthyCheck("message-broker", "broker connected")
            : HealthEndpointCheckResult.UnhealthyCheck("message-broker", "broker disconnected");

    public static HealthEndpointCheckResult CheckQueueDepth(FulfillmentHealthSnapshot snapshot)
        => snapshot.QueueDepth <= 100
            ? HealthEndpointCheckResult.HealthyCheck("queue-depth", "queue within target")
            : HealthEndpointCheckResult.UnhealthyCheck("queue-depth", "queue backlog above target");
}

[GenerateHealthEndpoint(typeof(FulfillmentHealthSnapshot), FactoryMethodName = "Create", EndpointName = "fulfillment-health")]
public static partial class GeneratedFulfillmentHealthEndpoint
{
    [HealthEndpointCheck("database", Order = 1)]
    private static HealthEndpointCheckResult CheckDatabase(FulfillmentHealthSnapshot snapshot)
        => FulfillmentHealthEndpoints.CheckDatabase(snapshot);

    [HealthEndpointCheck("message-broker", Order = 2)]
    private static HealthEndpointCheckResult CheckBroker(FulfillmentHealthSnapshot snapshot)
        => FulfillmentHealthEndpoints.CheckBroker(snapshot);

    [HealthEndpointCheck("queue-depth", Order = 3)]
    private static HealthEndpointCheckResult CheckQueueDepth(FulfillmentHealthSnapshot snapshot)
        => FulfillmentHealthEndpoints.CheckQueueDepth(snapshot);
}

public sealed class FulfillmentHealthEndpointDemoRunner(FulfillmentHealthEndpointService service)
{
    public FulfillmentHealthSummary RunGenerated() => service.Summarize();

    public static FulfillmentHealthSummary RunFluent()
        => RunWith(FulfillmentHealthEndpoints.CreateFluent(), HealthySnapshot());

    public static FulfillmentHealthSummary RunGeneratedStatic()
        => RunWith(GeneratedFulfillmentHealthEndpoint.Create(), HealthySnapshot());

    public static FulfillmentHealthSnapshot HealthySnapshot()
        => new(DatabaseOnline: true, BrokerOnline: true, QueueDepth: 8);

    public static FulfillmentHealthSnapshot DegradedSnapshot()
        => new(DatabaseOnline: true, BrokerOnline: false, QueueDepth: 175);

    private static FulfillmentHealthSummary RunWith(HealthEndpoint<FulfillmentHealthSnapshot> endpoint, FulfillmentHealthSnapshot snapshot)
    {
        var service = new FulfillmentHealthEndpointService(endpoint, new StaticFulfillmentHealthSnapshotProvider(snapshot));
        return service.Summarize();
    }
}

public static class FulfillmentHealthEndpointServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentHealthEndpointDemo(
        this IServiceCollection services,
        Func<IServiceProvider, FulfillmentHealthSnapshot>? snapshotFactory = null)
    {
        services.AddSingleton(static _ => GeneratedFulfillmentHealthEndpoint.Create());
        services.AddSingleton<IFulfillmentHealthSnapshotProvider>(sp => new StaticFulfillmentHealthSnapshotProvider(
            snapshotFactory?.Invoke(sp) ?? FulfillmentHealthEndpointDemoRunner.HealthySnapshot()));
        services.AddSingleton<FulfillmentHealthEndpointService>();
        services.AddSingleton<FulfillmentHealthEndpointDemoRunner>();
        return services;
    }
}

public static class FulfillmentHealthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFulfillmentHealthEndpoint(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/health/fulfillment")
    {
        endpoints.MapGet(pattern, static (FulfillmentHealthEndpointService service) =>
        {
            var report = service.Evaluate();
            return report.Healthy
                ? Results.Ok(report)
                : Results.Problem(
                    title: "Fulfillment health check failed",
                    detail: $"{report.FailedCount} health check(s) failed.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }).WithName("FulfillmentHealthEndpoint");

        return endpoints;
    }
}
