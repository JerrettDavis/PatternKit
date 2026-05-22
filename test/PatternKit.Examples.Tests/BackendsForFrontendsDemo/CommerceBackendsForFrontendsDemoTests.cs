using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.BackendsForFrontendsDemo;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.BackendsForFrontendsDemo;

[Feature("Commerce Backends for Frontends demo")]
public sealed class CommerceBackendsForFrontendsDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent BFF shapes client-specific responses")]
    [Fact]
    public Task Fluent_Bff_Shapes_Client_Specific_Responses()
        => Given("a fluent commerce BFF", () => CommerceBackendsForFrontends.CreateFluent(new DemoCommerceSummaryBackend()))
        .When("web and mobile clients request summaries", bff => new
        {
            Web = bff.Dispatch(new CommerceClientRequest("web", "C-100")),
            Mobile = bff.Dispatch(new CommerceClientRequest("mobile", "C-100"))
        })
        .Then("each response has the client-specific shape", result =>
        {
            ScenarioExpect.Equal("rich", result.Web.Response!.Shape);
            ScenarioExpect.True(result.Web.Response.IncludesPromotions);
            ScenarioExpect.Equal("compact", result.Mobile.Response!.Shape);
            ScenarioExpect.False(result.Mobile.Response.IncludesPromotions);
        })
        .AssertPassed();

    [Scenario("Generated BFF is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Bff_Is_Importable_Through_IServiceCollection()
        => Given("a service provider configured with the commerce BFF demo", () =>
        {
            var services = new ServiceCollection();
            services.AddCommerceBackendsForFrontendsDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the demo runner resolves and runs", provider =>
        {
            using (provider)
                return provider.GetRequiredService<CommerceBackendsForFrontendsDemoRunner>().RunGenerated("mobile", "C-100");
        })
        .Then("the generated BFF returns the expected response", response =>
        {
            ScenarioExpect.Equal("C-100", response.CustomerId);
            ScenarioExpect.Equal("compact", response.Shape);
            ScenarioExpect.False(response.IncludesPromotions);
        })
        .AssertPassed();

    [Scenario("Commerce BFF appears in production catalogs")]
    [Fact]
    public Task Commerce_Bff_Appears_In_Production_Catalogs()
        => Given("the production catalogs", () => new
        {
            Examples = new PatternKitExampleCatalog(),
            Patterns = new PatternKitPatternCatalog()
        })
        .Then("the example catalog includes the BFF demo", ctx =>
            ScenarioExpect.Contains(ctx.Examples.Entries, entry => entry.Name == "Commerce Backends for Frontends"))
        .And("the pattern catalog includes Backends for Frontends", ctx =>
            ScenarioExpect.Contains(ctx.Patterns.Patterns, pattern => pattern.Name == "Backends for Frontends"))
        .AssertPassed();

    [Scenario("Aggregate example registration includes commerce BFF")]
    [Fact]
    public Task Aggregate_Example_Registration_Includes_Commerce_Bff()
        => Given("all PatternKit examples registered in a service collection", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the BFF example is resolved", provider =>
        {
            using (provider)
                return provider.GetRequiredService<CommerceBackendsForFrontendsExample>().Runner.RunGenerated("web", "C-100");
        })
        .Then("the registered example executes through the generated BFF", response =>
        {
            ScenarioExpect.Equal("rich", response.Shape);
            ScenarioExpect.True(response.IncludesPromotions);
        })
        .AssertPassed();
}
