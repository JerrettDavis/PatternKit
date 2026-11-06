using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.AmbassadorDemo;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.AmbassadorDemo;

[Feature("Inventory Ambassador demo")]
public sealed class InventoryAmbassadorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent ambassador wraps outbound inventory calls")]
    [Fact]
    public Task Fluent_Ambassador_Wraps_Outbound_Inventory_Calls()
        => Given("a fluent inventory ambassador", () => InventoryAmbassadors.CreateFluent(new DemoInventoryAvailabilityClient()))
        .When("availability is requested", ambassador => ambassador.Invoke(new InventoryAmbassadorRequest("sku-1", "tenant-a")))
        .Then("the outbound call is transformed and completed", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("SKU-1", result.Response!.Sku);
            ScenarioExpect.Equal("inventory-api", result.Response.Source);
            ScenarioExpect.Contains("trace", result.Events);
        })
        .AssertPassed();

    [Scenario("Generated ambassador is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Ambassador_Is_Importable_Through_IServiceCollection()
        => Given("a service provider configured with the inventory ambassador demo", () =>
        {
            var services = new ServiceCollection();
            services.AddInventoryAmbassadorDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the demo runner resolves and runs", provider =>
        {
            using (provider)
                return provider.GetRequiredService<InventoryAmbassadorDemoRunner>().RunGenerated("tenant-a", "sku-1");
        })
        .Then("the generated ambassador returns the expected response", response =>
        {
            ScenarioExpect.Equal("SKU-1", response.Sku);
            ScenarioExpect.Equal("available", response.Status);
        })
        .AssertPassed();

    [Scenario("Inventory ambassador appears in production catalogs")]
    [Fact]
    public Task Inventory_Ambassador_Appears_In_Production_Catalogs()
        => Given("the production catalogs", () => new
        {
            Examples = new PatternKitExampleCatalog(),
            Patterns = new PatternKitPatternCatalog()
        })
        .Then("the example catalog includes the ambassador demo", ctx =>
            ScenarioExpect.Contains(ctx.Examples.Entries, entry => entry.Name == "Inventory Ambassador"))
        .And("the pattern catalog includes Ambassador", ctx =>
            ScenarioExpect.Contains(ctx.Patterns.Patterns, pattern => pattern.Name == "Ambassador"))
        .AssertPassed();

    [Scenario("Aggregate example registration includes inventory ambassador")]
    [Fact]
    public Task Aggregate_Example_Registration_Includes_Inventory_Ambassador()
        => Given("all PatternKit examples registered in a service collection", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the ambassador example is resolved", provider =>
        {
            using (provider)
                return provider.GetRequiredService<InventoryAmbassadorExample>().Runner.RunGenerated("blocked", "sku-1");
        })
        .Then("the registered example executes fallback behavior", response =>
        {
            ScenarioExpect.Equal("SKU-1", response.Sku);
            ScenarioExpect.Equal("cached", response.Status);
            ScenarioExpect.Equal("fallback-cache", response.Source);
        })
        .AssertPassed();
}
