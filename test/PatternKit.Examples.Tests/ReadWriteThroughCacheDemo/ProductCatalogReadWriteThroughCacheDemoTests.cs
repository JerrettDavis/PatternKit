using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.ReadWriteThroughCache;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ReadWriteThroughCacheDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.ReadWriteThroughCacheDemo;

public sealed class ProductCatalogReadWriteThroughCacheDemoTests
{
    [Scenario("Fluent and generated read write through cache policies coordinate catalog reads and writes")]
    [Fact]
    public async Task Fluent_And_Generated_ReadWriteThrough_Cache_Policies_Coordinate_Catalog_Reads_And_Writes()
    {
        var fluent = new ProductCatalogReadWriteThroughCacheService(
            new ProductCatalogReadWriteRepository(new CatalogProduct("SKU-42", "Trail Jacket", 129m)),
            ProductCatalogReadWriteThroughPolicies.CreateFluent());
        var generated = new ProductCatalogReadWriteThroughCacheService(
            new ProductCatalogReadWriteRepository(new CatalogProduct("SKU-42", "Trail Jacket", 129m)),
            GeneratedProductCatalogReadWriteThroughPolicy.CreateGenerated());

        var first = await fluent.FindAsync("SKU-42");
        var second = await fluent.FindAsync("SKU-42");
        var write = await generated.SaveAsync(new CatalogProduct("SKU-42", "Trail Jacket", 139m));
        var afterWrite = await generated.FindAsync("SKU-42");

        ScenarioExpect.False(first.CacheHit);
        ScenarioExpect.True(second.CacheHit);
        ScenarioExpect.Equal(1, second.OriginReads);
        ScenarioExpect.True(write.Written);
        ScenarioExpect.True(afterWrite.CacheHit);
        ScenarioExpect.Equal(139m, afterWrite.Product!.Price);
    }

    [Scenario("ServiceCollection imports read write through cache example")]
    [Fact]
    public async Task ServiceCollection_Imports_ReadWriteThrough_Cache_Example()
    {
        var services = new ServiceCollection();
        services.AddProductCatalogReadWriteThroughDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<ProductCatalogReadWriteThroughDemoRunner>();
        var results = await runner.RunAsync();

        ScenarioExpect.NotNull(provider.GetRequiredService<ReadWriteThroughCachePolicy<CatalogProduct>>());
        ScenarioExpect.Equal(4, results.Count);
        ScenarioExpect.True(results[1].CacheHit);
        ScenarioExpect.True(results[2].Written);
        ScenarioExpect.True(results[3].CacheHit);
    }

    [Scenario("Aggregate examples import read write through cache example")]
    [Fact]
    public async Task Aggregate_Examples_Import_ReadWriteThrough_Cache_Example()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<ProductCatalogReadWriteThroughExample>();
        var results = await example.Runner.RunAsync();

        ScenarioExpect.NotNull(example.Policy);
        ScenarioExpect.True(results[1].CacheHit);
        ScenarioExpect.True(results[2].Written);
    }
}
