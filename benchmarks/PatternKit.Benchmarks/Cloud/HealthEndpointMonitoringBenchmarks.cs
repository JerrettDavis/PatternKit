using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.HealthEndpointMonitoring;
using PatternKit.Examples.HealthEndpointMonitoringDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "HealthEndpointMonitoring")]
public class HealthEndpointMonitoringBenchmarks
{
    private static readonly FulfillmentHealthSnapshot Snapshot = FulfillmentHealthEndpointDemoRunner.HealthySnapshot();
    private readonly HealthEndpoint<FulfillmentHealthSnapshot> _fluent = FulfillmentHealthEndpoints.CreateFluent();
    private readonly HealthEndpoint<FulfillmentHealthSnapshot> _generated = GeneratedFulfillmentHealthEndpoint.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create health endpoint")]
    [BenchmarkCategory("Fluent", "Construction")]
    public HealthEndpoint<FulfillmentHealthSnapshot> Fluent_CreateEndpoint()
        => FulfillmentHealthEndpoints.CreateFluent();

    [Benchmark(Description = "Generated: create health endpoint")]
    [BenchmarkCategory("Generated", "Construction")]
    public HealthEndpoint<FulfillmentHealthSnapshot> Generated_CreateEndpoint()
        => GeneratedFulfillmentHealthEndpoint.Create();

    [Benchmark(Description = "Fluent: evaluate fulfillment health")]
    [BenchmarkCategory("Fluent", "Execution")]
    public HealthEndpointReport Fluent_EvaluateFulfillmentHealth()
        => _fluent.Evaluate(Snapshot);

    [Benchmark(Description = "Generated: evaluate fulfillment health")]
    [BenchmarkCategory("Generated", "Execution")]
    public HealthEndpointReport Generated_EvaluateFulfillmentHealth()
        => _generated.Evaluate(Snapshot);
}
