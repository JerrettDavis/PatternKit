using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.StranglerFig;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;
using PatternKit.Examples.StranglerFigDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.StranglerFigDemo;

[Feature("Checkout Strangler Fig example")]
public sealed class CheckoutStranglerFigDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent checkout migration routes modern and legacy traffic")]
    [Fact]
    public Task Fluent_Checkout_Migration_Routes_Modern_And_Legacy_Traffic()
        => Given("the fluent checkout migration", () => CheckoutMigrationRoutes.CreateFluent(new DemoLegacyCheckoutSystem(), new DemoModernCheckoutSystem()))
        .When("requests are submitted", migration => new
        {
            Modern = migration.Route(new CheckoutMigrationRequest("enterprise-west", "O-100", 100m)),
            Legacy = migration.Route(new CheckoutMigrationRequest("retail", "O-200", 25m))
        })
        .Then("traffic is routed by migration rules", result =>
        {
            ScenarioExpect.True(result.Modern.UsedModern);
            ScenarioExpect.Equal("modern-checkout", result.Modern.Response.Processor);
            ScenarioExpect.True(result.Legacy.UsedLegacy);
            ScenarioExpect.Equal("legacy-mainframe", result.Legacy.Response.Processor);
        })
        .AssertPassed();

    [Scenario("Generated checkout migration is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Checkout_Migration_Is_Importable_Through_IServiceCollection()
        => Given("a service collection with the checkout Strangler Fig example", () =>
        {
            var services = new ServiceCollection();
            services.AddCheckoutStranglerFigDemo();
            services.AddCheckoutStranglerFigExample();
            return services.BuildServiceProvider();
        })
        .When("the demo runner submits a pilot request", provider =>
        {
            var example = provider.GetRequiredService<CheckoutStranglerFigExample>();
            return example.Runner.RunGenerated(new CheckoutMigrationRequest("retail", "O-300", 1_500m));
        })
        .Then("the generated migration uses the modern implementation", result =>
        {
            ScenarioExpect.True(result.UsedModern);
            ScenarioExpect.Equal(StranglerFigRoute.Modern, result.Decision.Route);
            ScenarioExpect.Equal("large-order-pilot", result.Decision.RuleName);
        })
        .AssertPassed();

    [Scenario("Checkout Strangler Fig example is cataloged as production ready")]
    [Fact]
    public Task Checkout_Strangler_Fig_Example_Is_Cataloged_As_Production_Ready()
        => Given("the production readiness catalogs", () => new
        {
            Examples = new PatternKitExampleCatalog(),
            Patterns = new PatternKitPatternCatalog()
        })
        .Then("the example catalog includes the checkout migration", catalogs =>
            ScenarioExpect.Contains(catalogs.Examples.Entries, entry =>
                entry.Name == "Checkout Strangler Fig Migration"
                && entry.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)
                && entry.Integration.HasFlag(ExampleIntegrationSurface.AspNetCore)
                && entry.Integration.HasFlag(ExampleIntegrationSurface.SourceGenerator)))
        .And("the pattern catalog includes Strangler Fig", catalogs =>
            ScenarioExpect.Contains(catalogs.Patterns.Patterns, pattern =>
                pattern.Name == "Strangler Fig"
                && pattern.Implementation.HasSourceGeneratedPath))
        .AssertPassed();
}
