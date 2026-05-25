using BenchmarkDotNet.Attributes;
using PatternKit.Application.IdentityMap;
using PatternKit.Examples.IdentityMapDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "IdentityMap")]
public class IdentityMapBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create identity map")]
    [BenchmarkCategory("Fluent", "Construction")]
    public IIdentityMap<TrackedOrder, string> Fluent_CreateMap()
        => OrderIdentityMapPolicies.CreateFluentMap();

    [Benchmark(Description = "Generated: create identity map")]
    [BenchmarkCategory("Generated", "Construction")]
    public IIdentityMap<TrackedOrder, string> Generated_CreateMap()
        => GeneratedOrderIdentityMap.CreateMap();

    [Benchmark(Description = "Fluent: load tracked order twice")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderIdentityMapSummary Fluent_LoadTrackedOrderTwice()
        => OrderIdentityMapDemo.RunFluent();

    [Benchmark(Description = "Generated: load tracked order twice")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderIdentityMapSummary Generated_LoadTrackedOrderTwice()
        => OrderIdentityMapDemo.RunGenerated();
}
