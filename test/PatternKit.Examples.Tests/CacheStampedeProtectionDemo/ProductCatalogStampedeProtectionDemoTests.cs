using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.CacheStampedeProtection;
using PatternKit.Examples.CacheStampedeProtectionDemo;
using PatternKit.Examples.DependencyInjection;
using TinyBDD;

namespace PatternKit.Examples.Tests.CacheStampedeProtectionDemo;

public sealed class ProductCatalogStampedeProtectionDemoTests
{
    [Scenario("Fluent cache stampede protection shares product catalog loads")]
    [Fact]
    public async Task Fluent_Cache_Stampede_Protection_Shares_Product_Catalog_Loads()
    {
        var results = await ProductCatalogStampedeProtectionDemoRunner.RunFluentAsync(CreateRequest());

        ScenarioExpect.Equal(2, results.Count);
        ScenarioExpect.Equal(1, results.Max(static result => result.OriginLoadCount));
        ScenarioExpect.Contains(results, static result => result.SharedFlight);
    }

    [Scenario("Generated cache stampede protection matches fluent behavior")]
    [Fact]
    public async Task Generated_Cache_Stampede_Protection_Matches_Fluent_Behavior()
    {
        var request = CreateRequest();

        var fluent = await ProductCatalogStampedeProtectionDemoRunner.RunFluentAsync(request);
        var generated = await ProductCatalogStampedeProtectionDemoRunner.RunGeneratedStaticAsync(request);

        ScenarioExpect.Equal(fluent.Count, generated.Count);
        ScenarioExpect.Equal(1, generated.Max(static result => result.OriginLoadCount));
        ScenarioExpect.Contains(generated, static result => result.SharedFlight);
    }

    [Scenario("Product catalog stampede protection service validates requests")]
    [Fact]
    public async Task Product_Catalog_Stampede_Protection_Service_Validates_Requests()
    {
        var service = new ProductCatalogStampedeProtectionService(
            ProductCatalogStampedeProtectionPolicies.CreateFluent(),
            new ProductCatalogOrigin());

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await service.GetAvailabilityAsync(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(async () => await new ProductCatalogOrigin().LoadAsync(null!, CancellationToken.None));
    }

    [Scenario("ServiceCollection imports cache stampede protection example")]
    [Fact]
    public async Task ServiceCollection_Imports_Cache_Stampede_Protection_Example()
    {
        var services = new ServiceCollection();
        services.AddProductCatalogStampedeProtectionDemo();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<ProductCatalogStampedeProtectionDemoRunner>();
        var results = await runner.RunGeneratedAsync(CreateRequest());

        ScenarioExpect.Equal(1, results.Max(static result => result.OriginLoadCount));
        ScenarioExpect.NotNull(provider.GetRequiredService<CacheStampedeProtectionPolicy<ProductAvailabilitySnapshot>>());
    }

    [Scenario("Aggregate examples import cache stampede protection example")]
    [Fact]
    public async Task Aggregate_Examples_Import_Cache_Stampede_Protection_Example()
    {
        var services = new ServiceCollection();
        services.AddPatternKitExamples();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var example = provider.GetRequiredService<ProductCatalogStampedeProtectionExample>();
        var results = await example.Runner.RunGeneratedAsync(CreateRequest());

        ScenarioExpect.Equal(1, results.Max(static result => result.OriginLoadCount));
        ScenarioExpect.NotNull(example.Policy);
    }

    private static ProductAvailabilityRequest CreateRequest()
        => new("SKU-100", "us");
}
