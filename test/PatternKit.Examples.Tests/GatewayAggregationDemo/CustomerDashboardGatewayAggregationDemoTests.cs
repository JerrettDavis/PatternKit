using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.GatewayAggregationDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.GatewayAggregationDemo;

[Feature("Customer Dashboard Gateway Aggregation example")]
public sealed class CustomerDashboardGatewayAggregationDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated paths aggregate a customer dashboard")]
    [Fact]
    public Task Fluent_And_Generated_Paths_Aggregate_A_Customer_Dashboard()
        => Given("a customer id", () => "C-100")
        .When("fluent and generated gateways aggregate the dashboard", customerId => new
        {
            Fluent = CustomerDashboardGatewayAggregationDemoRunner.RunFluent(),
            Generated = BuildServiceProvider().GetRequiredService<CustomerDashboardGatewayAggregationDemoRunner>().RunGenerated(customerId)
        })
        .Then("both paths compose the downstream response", result =>
        {
            ScenarioExpect.Equal("C-100", result.Fluent.CustomerId);
            ScenarioExpect.Equal("Ada Lovelace", result.Generated.Name);
            ScenarioExpect.Equal(2, result.Generated.OpenOrders);
            ScenarioExpect.Equal(4, result.Generated.RecommendedProducts);
        })
        .AssertPassed();

    [Scenario("Gateway aggregation is importable through AddPatternKitExamples")]
    [Fact]
    public Task Gateway_Aggregation_Is_Importable_Through_AddPatternKitExamples()
        => Given("the aggregate PatternKit example registration", () => new ServiceCollection().AddPatternKitExamples().BuildServiceProvider())
        .When("the gateway aggregation example is resolved", provider => provider.GetRequiredService<CustomerDashboardGatewayAggregationExample>())
        .Then("the runner and service are available through standard IoC", example =>
        {
            var dashboard = example.Runner.RunGenerated("C-200");
            ScenarioExpect.Equal("C-200", dashboard.CustomerId);
            ScenarioExpect.NotNull(example.Service);
        })
        .AssertPassed();

    private static ServiceProvider BuildServiceProvider()
        => new ServiceCollection()
            .AddCustomerDashboardGatewayAggregationDemo()
            .BuildServiceProvider();
}
