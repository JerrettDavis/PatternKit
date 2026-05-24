using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.RateLimiting;
using PatternKit.Examples.RateLimitingDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "RateLimiting")]
public class RateLimitingBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create rate limit policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public RateLimitPolicy<SearchResponse> Fluent_CreatePolicy()
        => ProductSearchRateLimitPolicies.CreateFluentPolicy();

    [Benchmark(Description = "Generated: create rate limit policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public RateLimitPolicy<SearchResponse> Generated_CreatePolicy()
        => GeneratedProductSearchRateLimitPolicy.CreateGeneratedPolicy();

    [Benchmark(Description = "Fluent: reject third tenant request")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<ProductSearchRateLimitResult> Fluent_RejectThirdTenantRequest()
    {
        var service = new ProductSearchRateLimitService(
            new ScriptedProductSearchService(),
            ProductSearchRateLimitPolicies.CreateFluentPolicy());

        _ = await service.SearchAsync("tenant-a", "boots");
        _ = await service.SearchAsync("tenant-a", "coats");
        return await service.SearchAsync("tenant-a", "gloves");
    }

    [Benchmark(Description = "Generated: reject third tenant request")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<ProductSearchRateLimitResult> Generated_RejectThirdTenantRequest()
    {
        var service = new ProductSearchRateLimitService(
            new ScriptedProductSearchService(),
            GeneratedProductSearchRateLimitPolicy.CreateGeneratedPolicy());

        _ = await service.SearchAsync("tenant-a", "boots");
        _ = await service.SearchAsync("tenant-a", "coats");
        return await service.SearchAsync("tenant-a", "gloves");
    }
}
