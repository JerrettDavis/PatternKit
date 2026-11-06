using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.GatewayRoutingDemo;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.GatewayRoutingDemo;

[Feature("Product Gateway Routing example")]
public sealed class ProductGatewayRoutingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent product gateway routes requests by path")]
    [Fact]
    public Task Fluent_Product_Gateway_Routes_Requests_By_Path()
        => Given("the fluent product gateway router", () => ProductGatewayRoutes.CreateFluent(new DemoProductInventoryApi(), new DemoProductPricingApi()))
        .When("requests are routed", router => new
        {
            Inventory = router.Route(new ProductGatewayRequest("/inventory/SKU-100", "tenant-a")),
            Pricing = router.Route(new ProductGatewayRequest("/pricing/SKU-100", "tenant-a")),
            Fallback = router.Route(new ProductGatewayRequest("/unknown/SKU-100", "tenant-a"))
        })
        .Then("matching traffic uses downstream APIs and unknown traffic uses fallback", result =>
        {
            ScenarioExpect.Equal("inventory", result.Inventory.RouteName);
            ScenarioExpect.Equal("inventory", result.Inventory.Response.Source);
            ScenarioExpect.Equal("pricing", result.Pricing.RouteName);
            ScenarioExpect.Equal("pricing", result.Pricing.Response.Source);
            ScenarioExpect.True(result.Fallback.Fallback);
            ScenarioExpect.Equal("fallback", result.Fallback.Response.Source);
        })
        .AssertPassed();

    [Scenario("Generated product gateway routing is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Product_Gateway_Routing_Is_Importable_Through_IServiceCollection()
        => Given("a service collection with the gateway routing example", () =>
        {
            var services = new ServiceCollection();
            services.AddProductGatewayRoutingExample();
            return services.BuildServiceProvider();
        })
        .When("the demo runner routes a pricing request", provider => provider.GetRequiredService<ProductGatewayRoutingExample>().Runner.RunGenerated(new ProductGatewayRequest("/pricing/SKU-100", "tenant-a")))
        .Then("the generated router uses the pricing route", result =>
        {
            ScenarioExpect.Equal("pricing", result.RouteName);
            ScenarioExpect.Equal("pricing", result.Response.Source);
        })
        .AssertPassed();

    [Scenario("Product Gateway Routing example is cataloged as production ready")]
    [Fact]
    public Task Product_Gateway_Routing_Example_Is_Cataloged_As_Production_Ready()
        => Given("the production readiness catalogs", () => new { Examples = new PatternKitExampleCatalog(), Patterns = new PatternKitPatternCatalog() })
        .Then("the example catalog includes product gateway routing", catalogs =>
            ScenarioExpect.Contains(catalogs.Examples.Entries, entry => entry.Name == "Product Gateway Routing" && entry.Integration.HasFlag(ExampleIntegrationSurface.AspNetCore)))
        .And("the pattern catalog includes Gateway Routing", catalogs =>
            ScenarioExpect.Contains(catalogs.Patterns.Patterns, pattern => pattern.Name == "Gateway Routing" && pattern.Implementation.HasSourceGeneratedPath))
        .AssertPassed();
}
