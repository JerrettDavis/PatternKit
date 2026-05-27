using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Order message history example")]
public sealed class OrderMessageHistoryExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent message history captures order handling steps")]
    [Fact]
    public Task Fluent_MessageHistory_Captures_Order_Handling_Steps()
        => Given("an order imported through the fluent history path", () => new HistoryOrder("O-100", 125m, "web"))
            .When("the example runs", OrderMessageHistoryExampleRunner.RunFluent)
            .Then("both production components are recorded", result =>
            {
                ScenarioExpect.Equal("O-100", result.OrderId);
                ScenarioExpect.Equal(2, result.HistoryCount);
                ScenarioExpect.Equal(["checkout-api", "fulfillment-router"], result.Components);
            })
            .And("the original correlation id is preserved", result =>
                ScenarioExpect.Equal("order:O-100", result.CorrelationId))
            .AssertPassed();

    [Scenario("Generated message history matches fluent behavior")]
    [Fact]
    public Task Generated_MessageHistory_Matches_Fluent_Behavior()
        => Given("an order imported through the generated history path", () => new HistoryOrder("O-101", 250m, "store"))
            .When("the example runs", OrderMessageHistoryExampleRunner.RunGeneratedStatic)
            .Then("both production components are recorded", result =>
                ScenarioExpect.Equal(["checkout-api", "fulfillment-router"], result.Components))
            .And("the history count matches the fluent path", result =>
                ScenarioExpect.Equal(2, result.HistoryCount))
            .AssertPassed();

    [Scenario("Message history example is importable through IServiceCollection")]
    [Fact]
    public Task MessageHistory_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection with the message history demo", () =>
            {
                var services = new ServiceCollection();
                services.AddOrderMessageHistoryDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("the runner is resolved and executed", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<OrderMessageHistoryExampleRunner>()
                        .RunGenerated(new("O-102", 75m, "mobile"));
            })
            .Then("the generated DI registrations capture history", result =>
                ScenarioExpect.Equal(["checkout-api", "fulfillment-router"], result.Components))
            .AssertPassed();

    [Scenario("Message history is included in aggregate PatternKit examples")]
    [Fact]
    public Task MessageHistory_Is_Included_In_Aggregate_PatternKit_Examples()
        => Given("the aggregate PatternKit examples registration", () =>
            {
                var services = new ServiceCollection();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("the message history runner is resolved", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<OrderMessageHistoryExampleRunner>();
            })
            .Then("the runner is available", runner =>
                ScenarioExpect.NotNull(runner))
            .AssertPassed();
}
