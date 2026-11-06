using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.CacheStampedeProtection;
using PatternKit.Examples.CacheStampedeProtectionDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "CacheStampedeProtection")]
public class CacheStampedeProtectionBenchmarks
{
    private static readonly ProductAvailabilityRequest Request = new("SKU-100", "us");

    [Benchmark(Baseline = true, Description = "Fluent: create cache stampede protection policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot> Fluent_CreatePolicy()
        => ProductCatalogStampedeProtectionPolicies.CreateFluent();

    [Benchmark(Description = "Generated: create cache stampede protection policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot> Generated_CreatePolicy()
        => GeneratedProductCatalogStampedeProtectionPolicy.CreateGenerated();

    [Benchmark(Description = "Fluent: share product catalog load")]
    [BenchmarkCategory("Fluent", "Execution")]
    public IReadOnlyList<ProductAvailabilitySummary> Fluent_ShareProductCatalogLoad()
        => ProductCatalogStampedeProtectionDemoRunner.RunFluentAsync(Request).AsTask().GetAwaiter().GetResult();

    [Benchmark(Description = "Generated: share product catalog load")]
    [BenchmarkCategory("Generated", "Execution")]
    public IReadOnlyList<ProductAvailabilitySummary> Generated_ShareProductCatalogLoad()
        => ProductCatalogStampedeProtectionDemoRunner.RunGeneratedStaticAsync(Request).AsTask().GetAwaiter().GetResult();
}
