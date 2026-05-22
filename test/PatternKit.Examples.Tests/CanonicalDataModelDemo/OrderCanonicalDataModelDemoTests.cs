using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.CanonicalDataModelDemo;
using PatternKit.Examples.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.CanonicalDataModelDemo;

[Feature("Order canonical data model example")]
public sealed class OrderCanonicalDataModelDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated canonical data models normalize partner orders")]
    [Fact]
    public Task Fluent_And_Generated_Canonical_Data_Models_Normalize_Partner_Orders()
        => Given("canonical order examples", () => new
        {
            Fluent = CanonicalOrderDemoRunner.RunFluent(),
            Generated = CanonicalOrderDemoRunner.RunGeneratedStatic()
        })
        .Then("both paths produce canonical commerce orders", result =>
        {
            ScenarioExpect.Equal("commerce-orders", result.Generated.ModelName);
            ScenarioExpect.Equal("partner-orders", result.Generated.AdapterName);
            ScenarioExpect.Equal("P-100", result.Generated.OrderId);
            ScenarioExpect.Equal("USD", result.Generated.Currency);
            ScenarioExpect.Equal("marketplace-orders", result.Fluent.AdapterName);
        })
        .AssertPassed();

    [Scenario("Canonical data model demo is importable through IServiceCollection")]
    [Fact]
    public Task Canonical_Data_Model_Demo_Is_Importable_Through_IServiceCollection()
        => Given("an importing app service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddCanonicalOrderDataModelDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the import service", provider =>
        {
            using (provider)
                return provider.GetRequiredService<CanonicalOrderImportService>().ImportPartnerOrder(new("P-200", 18m, "usd"));
        })
        .Then("the service returns a canonical order summary", summary =>
        {
            ScenarioExpect.Equal("P-200", summary.OrderId);
            ScenarioExpect.Equal(18m, summary.Total);
            ScenarioExpect.Equal("USD", summary.Currency);
        })
        .AssertPassed();

    [Scenario("Aggregate examples import canonical data model demo")]
    [Fact]
    public Task Aggregate_Examples_Import_Canonical_Data_Model_Demo()
        => Given("a PatternKit examples service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving the aggregate canonical data model example", provider =>
        {
            using (provider)
                return provider.GetRequiredService<CanonicalOrderDataModelExample>();
        })
        .Then("the aggregate example exposes the service", example =>
            ScenarioExpect.NotNull(example.Service))
        .AssertPassed();
}
