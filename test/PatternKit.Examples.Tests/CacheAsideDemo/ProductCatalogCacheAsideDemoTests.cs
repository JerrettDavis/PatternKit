using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.CacheAsideDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.CacheAsideDemo;

[Feature("Product catalog cache-aside demo")]
public sealed class ProductCatalogCacheAsideDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated cache-aside policies reuse product catalog reads")]
    [Fact]
    public Task Fluent_And_Generated_CacheAside_Policies_Reuse_Product_Catalog_Reads()
        => Given("product catalog repositories imported by an application", static () => new
            {
                FluentRepository = new ScriptedProductCatalogRepository(new ProductReadModel("SKU-42", "Trail Jacket", 129m, true)),
                GeneratedRepository = new ScriptedProductCatalogRepository(new ProductReadModel("SKU-42", "Trail Jacket", 129m, true))
            })
            .When("looking up the same product twice through both policy paths", ctx =>
            {
                var fluent = new ProductCatalogCacheAsideService(ctx.FluentRepository, ProductCatalogCacheAsidePolicies.CreateFluentPolicy());
                var generated = new ProductCatalogCacheAsideService(ctx.GeneratedRepository, GeneratedProductCatalogCacheAsidePolicy.CreateGeneratedPolicy());

                return new
                {
                    FluentFirst = fluent.FindAsync("SKU-42").GetAwaiter().GetResult(),
                    FluentSecond = fluent.FindAsync("SKU-42").GetAwaiter().GetResult(),
                    GeneratedFirst = generated.FindAsync("SKU-42").GetAwaiter().GetResult(),
                    GeneratedSecond = generated.FindAsync("SKU-42").GetAwaiter().GetResult(),
                    ctx.FluentRepository.Calls,
                    GeneratedCalls = ctx.GeneratedRepository.Calls
                };
            })
            .Then("both paths load once and then serve a cache hit", result =>
            {
                ScenarioExpect.True(result.FluentFirst.Found);
                ScenarioExpect.False(result.FluentFirst.CacheHit);
                ScenarioExpect.True(result.FluentSecond.CacheHit);
                ScenarioExpect.True(result.GeneratedSecond.CacheHit);
                ScenarioExpect.Equal("Trail Jacket", result.GeneratedSecond.Product?.Name);
            })
            .And("origin repositories are called once per policy path", result =>
            {
                ScenarioExpect.Equal(1, result.Calls);
                ScenarioExpect.Equal(1, result.GeneratedCalls);
            })
            .AssertPassed();

    [Scenario("Cache-aside demo does not cache inactive products")]
    [Fact]
    public async Task CacheAside_Demo_Does_Not_Cache_Inactive_Products()
    {
        var repository = new ScriptedProductCatalogRepository(new ProductReadModel("SKU-99", "Retired Boot", 89m, false));
        var service = new ProductCatalogCacheAsideService(repository, GeneratedProductCatalogCacheAsidePolicy.CreateGeneratedPolicy());

        var first = await service.FindAsync("SKU-99");
        var second = await service.FindAsync("SKU-99");

        ScenarioExpect.True(first.Found);
        ScenarioExpect.False(first.CacheHit);
        ScenarioExpect.False(second.CacheHit);
        ScenarioExpect.Equal(2, repository.Calls);
    }

    [Scenario("Product catalog cache-aside demo registers with IServiceCollection")]
    [Fact]
    public Task Product_Catalog_CacheAside_Demo_Registers_With_IServiceCollection()
        => Given("a standard service collection", static () =>
            {
                var services = new ServiceCollection();
                services.AddProductCatalogCacheAsideDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the product catalog cache-aside service", provider =>
            {
                using (provider)
                {
                    var service = provider.GetRequiredService<ProductCatalogCacheAsideService>();
                    _ = service.FindAsync("SKU-42").GetAwaiter().GetResult();
                    return service.FindAsync("SKU-42").GetAwaiter().GetResult();
                }
            })
            .Then("the registered service uses the generated cache-aside policy", result =>
            {
                ScenarioExpect.True(result.Found);
                ScenarioExpect.True(result.CacheHit);
                ScenarioExpect.Equal("Trail Jacket", result.Product?.Name);
            })
            .AssertPassed();
}
