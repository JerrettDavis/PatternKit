using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Fulfillment competing consumers example")]
public sealed class FulfillmentCompetingConsumersExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record CompetingConsumerSummaries(
        FulfillmentCompetingConsumersSummary Fluent,
        FulfillmentCompetingConsumersSummary Generated);

    [Scenario("Fluent and generated competing consumers dispatch fulfillment work")]
    [Fact]
    public Task Fluent_And_Generated_Competing_Consumers_Dispatch_Fulfillment_Work()
        => Given("fulfillment competing consumer examples", RunBothExamplesAsync)
        .Then("both paths distribute work across consumers", result =>
        {
            ScenarioExpect.True(result.Fluent.Accepted);
            ScenarioExpect.True(result.Generated.Accepted);
            ScenarioExpect.Contains("east-worker", result.Fluent.Consumers);
            ScenarioExpect.Contains("west-worker", result.Generated.Consumers);
            ScenarioExpect.Equal("fulfillment-competing-consumers", result.Generated.GroupName);
        })
        .AssertPassed();

    [Scenario("Competing consumers demo is importable through IServiceCollection")]
    [Fact]
    public Task Competing_Consumers_Demo_Is_Importable_Through_IServiceCollection()
        => Given("an importing app service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddFulfillmentCompetingConsumersDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the service", provider =>
        {
            using (provider)
            {
                var service = provider.GetRequiredService<FulfillmentCompetingConsumerService>();
                return service.DispatchAsync(new FulfillmentConsumerWork("order-di", "central")).AsTask();
            }
        })
        .Then("the service dispatches the work", result =>
        {
            ScenarioExpect.True(result.Accepted);
            ScenarioExpect.Equal("order-di", result.Value?.OrderId);
            ScenarioExpect.Equal("east-worker", result.ConsumerName);
        })
        .AssertPassed();

    [Scenario("Competing consumers example is registered in the aggregate service collection")]
    [Fact]
    public Task Competing_Consumers_Example_Is_Registered_In_The_Aggregate_Service_Collection()
        => Given("the aggregate PatternKit examples service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving the competing consumers example", provider =>
        {
            using (provider)
                return provider.GetRequiredService<FulfillmentCompetingConsumersExampleService>();
        })
        .Then("the example exposes a runnable group", example =>
        {
            ScenarioExpect.Equal("fulfillment-competing-consumers", example.Group.Name);
            ScenarioExpect.Equal(2, example.Group.ConsumerCount);
        })
        .AssertPassed();

    private static async Task<CompetingConsumerSummaries> RunBothExamplesAsync()
        => new(
            await FulfillmentCompetingConsumersExample.RunFluentAsync(),
            await FulfillmentCompetingConsumersExample.RunGeneratedAsync());
}
