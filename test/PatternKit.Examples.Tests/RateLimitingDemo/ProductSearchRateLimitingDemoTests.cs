using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.RateLimitingDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.RateLimitingDemo;

[Feature("Product search rate limiting demo")]
public sealed class ProductSearchRateLimitingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent product search rate limit policy rejects tenant overflow")]
    [Fact]
    public Task Fluent_Product_Search_Rate_Limit_Policy_Rejects_Tenant_Overflow()
        => Given("a product search service protected by the fluent policy", () =>
            {
                var search = new ScriptedProductSearchService();
                var service = new ProductSearchRateLimitService(search, ProductSearchRateLimitPolicies.CreateFluentPolicy());
                return new SearchFixture(search, service);
            })
            .When("one tenant exceeds its allowed search budget", RunTenantOverflowAsync)
            .Then("the first two searches are allowed", result =>
                result.First.Allowed && result.Second.Allowed)
            .And("the overflow search is rejected without calling origin search", result =>
                result.Third.Rejected && result.Search.Calls == 2)
            .AssertPassed();

    [Scenario("Generated product search rate limit policy partitions tenants")]
    [Fact]
    public Task Generated_Product_Search_Rate_Limit_Policy_Partitions_Tenants()
        => Given("a product search service protected by the generated policy", () =>
            {
                var search = new ScriptedProductSearchService();
                var service = new ProductSearchRateLimitService(search, GeneratedProductSearchRateLimitPolicy.CreateGeneratedPolicy());
                return new SearchFixture(search, service);
            })
            .When("two tenants search independently", RunPartitionedSearchAsync)
            .Then("tenant A is throttled after its window budget", result =>
                result.TenantAFirst.Allowed && result.TenantASecond.Allowed && result.TenantAThird.Rejected)
            .And("tenant B still has an independent budget", result =>
                result.TenantBFirst.Allowed)
            .And("origin search only runs for allowed requests", result =>
                result.Search.Calls == 3)
            .AssertPassed();

    [Scenario("Product search rate limiting demo is importable through IServiceCollection")]
    [Fact]
    public Task Product_Search_Rate_Limiting_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service collection importing the product search rate limiting demo", () =>
            {
                var services = new ServiceCollection();
                services.AddProductSearchRateLimitingDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the registered service", RunImportedDemoAsync)
            .Then("the DI-owned generated policy rejects overflow", result =>
                result.Rejected)
            .AssertPassed();

    private static async Task<TenantOverflowResult> RunTenantOverflowAsync(SearchFixture fixture)
    {
        var first = await fixture.Service.SearchAsync("tenant-a", "boots");
        var second = await fixture.Service.SearchAsync("tenant-a", "jackets");
        var third = await fixture.Service.SearchAsync("tenant-a", "hats");
        return new(fixture.Search, first, second, third);
    }

    private static async Task<PartitionedSearchResult> RunPartitionedSearchAsync(SearchFixture fixture)
    {
        var tenantAFirst = await fixture.Service.SearchAsync("tenant-a", "boots");
        var tenantASecond = await fixture.Service.SearchAsync("tenant-a", "jackets");
        var tenantAThird = await fixture.Service.SearchAsync("tenant-a", "hats");
        var tenantBFirst = await fixture.Service.SearchAsync("tenant-b", "hats");
        return new(fixture.Search, tenantAFirst, tenantASecond, tenantAThird, tenantBFirst);
    }

    private static async Task<ProductSearchRateLimitResult> RunImportedDemoAsync(ServiceProvider provider)
    {
        using (provider)
        {
            var service = provider.GetRequiredService<ProductSearchRateLimitService>();
            _ = await service.SearchAsync("tenant-a", "boots");
            _ = await service.SearchAsync("tenant-a", "jackets");
            return await service.SearchAsync("tenant-a", "hats");
        }
    }

    private sealed record SearchFixture(ScriptedProductSearchService Search, ProductSearchRateLimitService Service);

    private sealed record TenantOverflowResult(
        ScriptedProductSearchService Search,
        ProductSearchRateLimitResult First,
        ProductSearchRateLimitResult Second,
        ProductSearchRateLimitResult Third);

    private sealed record PartitionedSearchResult(
        ScriptedProductSearchService Search,
        ProductSearchRateLimitResult TenantAFirst,
        ProductSearchRateLimitResult TenantASecond,
        ProductSearchRateLimitResult TenantAThird,
        ProductSearchRateLimitResult TenantBFirst);
}
