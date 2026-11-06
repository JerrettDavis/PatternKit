using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.CacheAside;
using PatternKit.Examples.CacheAsideDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "CacheAside")]
public class CacheAsideBenchmarks
{
    private static readonly ProductReadModel ActiveProduct = new("SKU-42", "Trail Jacket", 129m, true);

    [Benchmark(Baseline = true, Description = "Fluent: create cache-aside policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public CacheAsidePolicy<ProductReadModel> Fluent_CreatePolicy()
        => ProductCatalogCacheAsidePolicies.CreateFluentPolicy();

    [Benchmark(Description = "Generated: create cache-aside policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public CacheAsidePolicy<ProductReadModel> Generated_CreatePolicy()
        => GeneratedProductCatalogCacheAsidePolicy.CreateGeneratedPolicy();

    [Benchmark(Description = "Fluent: miss then cache hit")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<ProductCatalogLookup> Fluent_MissThenHit()
    {
        var service = new ProductCatalogCacheAsideService(
            new ScriptedProductCatalogRepository(ActiveProduct),
            ProductCatalogCacheAsidePolicies.CreateFluentPolicy());

        _ = await service.FindAsync("SKU-42");
        return await service.FindAsync("SKU-42");
    }

    [Benchmark(Description = "Generated: miss then cache hit")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<ProductCatalogLookup> Generated_MissThenHit()
    {
        var service = new ProductCatalogCacheAsideService(
            new ScriptedProductCatalogRepository(ActiveProduct),
            GeneratedProductCatalogCacheAsidePolicy.CreateGeneratedPolicy());

        _ = await service.FindAsync("SKU-42");
        return await service.FindAsync("SKU-42");
    }
}
