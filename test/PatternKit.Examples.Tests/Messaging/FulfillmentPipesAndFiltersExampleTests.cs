using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Fulfillment pipes and filters example")]
public sealed class FulfillmentPipesAndFiltersExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record PipelineSummaries(FulfillmentPipesAndFiltersSummary Fluent, FulfillmentPipesAndFiltersSummary Generated);

    [Scenario("Fluent and generated pipes and filters pipelines process fulfillment work")]
    [Fact]
    public Task Fluent_And_Generated_Pipes_And_Filters_Pipelines_Process_Fulfillment_Work()
        => Given("fulfillment pipes and filters examples", RunBothExamplesAsync)
        .Then("both paths publish fulfillment work", result =>
        {
            ScenarioExpect.True(result.Fluent.Published);
            ScenarioExpect.True(result.Generated.Published);
            ScenarioExpect.Equal(["validate", "reserve", "publish"], result.Generated.Filters);
        })
        .AssertPassed();

    [Scenario("Pipes and filters demo is importable through IServiceCollection")]
    [Fact]
    public Task Pipes_And_Filters_Demo_Is_Importable_Through_IServiceCollection()
        => Given("an importing app service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddFulfillmentPipesAndFiltersDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the service", provider =>
        {
            using (provider)
            {
                var service = provider.GetRequiredService<FulfillmentPipesAndFiltersService>();
                return service.ProcessAsync("order-di").AsTask();
            }
        })
        .Then("the service publishes the work", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.True(result.Value.Published);
        })
        .AssertPassed();

    [Scenario("Pipes and filters example is registered in the aggregate service collection")]
    [Fact]
    public Task Pipes_And_Filters_Example_Is_Registered_In_The_Aggregate_Service_Collection()
        => Given("the aggregate PatternKit examples service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving the pipes and filters example", provider =>
        {
            using (provider)
                return provider.GetRequiredService<FulfillmentPipesAndFiltersExampleService>();
        })
        .Then("the example exposes a runnable pipeline", example =>
        {
            ScenarioExpect.Equal("fulfillment-pipes", example.Pipeline.Name);
            ScenarioExpect.Equal(3, example.Pipeline.FilterCount);
        })
        .AssertPassed();

    private static async Task<PipelineSummaries> RunBothExamplesAsync()
        => new(
            await FulfillmentPipesAndFiltersExample.RunFluentAsync(),
            await FulfillmentPipesAndFiltersExample.RunGeneratedAsync());
}
