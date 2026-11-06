using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.ReadWriteThroughCache;
using PatternKit.Examples.ReadWriteThroughCacheDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "ReadWriteThroughCache")]
public class ReadWriteThroughCacheBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create read/write-through cache policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ReadWriteThroughCachePolicy<CatalogProduct> Fluent_CreatePolicy()
        => ProductCatalogReadWriteThroughPolicies.CreateFluent();

    [Benchmark(Description = "Generated: create read/write-through cache policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public ReadWriteThroughCachePolicy<CatalogProduct> Generated_CreatePolicy()
        => GeneratedProductCatalogReadWriteThroughPolicy.CreateGenerated();

    [Benchmark(Description = "Fluent: read/write-through product catalog flow")]
    [BenchmarkCategory("Fluent", "Execution")]
    public IReadOnlyList<ProductCatalogReadWriteSummary> Fluent_RunCatalogFlow()
    {
        var service = new ProductCatalogReadWriteThroughCacheService(
            new ProductCatalogReadWriteRepository(new CatalogProduct("SKU-42", "Trail Jacket", 129m)),
            ProductCatalogReadWriteThroughPolicies.CreateFluent());
        return new ProductCatalogReadWriteThroughDemoRunner(service).RunAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Description = "Generated: read/write-through product catalog flow")]
    [BenchmarkCategory("Generated", "Execution")]
    public IReadOnlyList<ProductCatalogReadWriteSummary> Generated_RunCatalogFlow()
    {
        var service = new ProductCatalogReadWriteThroughCacheService(
            new ProductCatalogReadWriteRepository(new CatalogProduct("SKU-42", "Trail Jacket", 129m)),
            GeneratedProductCatalogReadWriteThroughPolicy.CreateGenerated());
        return new ProductCatalogReadWriteThroughDemoRunner(service).RunAsync().AsTask().GetAwaiter().GetResult();
    }
}
