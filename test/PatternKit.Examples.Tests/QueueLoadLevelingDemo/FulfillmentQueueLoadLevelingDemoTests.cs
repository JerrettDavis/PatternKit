using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.QueueLoadLevelingDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.QueueLoadLevelingDemo;

[Feature("Fulfillment queue load leveling example")]
public sealed class FulfillmentQueueLoadLevelingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record QueueLoadLevelingSummaries(
        FulfillmentQueueSummary Fluent,
        FulfillmentQueueSummary Generated);

    [Scenario("Fluent and generated queue load leveling policies accept fulfillment work")]
    [Fact]
    public Task Fluent_And_Generated_Queue_Load_Leveling_Policies_Accept_Fulfillment_Work()
        => Given("fulfillment queue load leveling examples", RunBothExamplesAsync)
        .Then("both paths process work", result =>
        {
            ScenarioExpect.True(result.Fluent.Accepted);
            ScenarioExpect.True(result.Generated.Accepted);
            ScenarioExpect.Equal(2, result.Fluent.ProcessedCount);
            ScenarioExpect.Equal("fulfillment-queue", result.Generated.PolicyName);
        })
        .AssertPassed();

    private static async Task<QueueLoadLevelingSummaries> RunBothExamplesAsync()
        => new(
            await FulfillmentQueueLoadLevelingDemo.RunFluentAsync(),
            await FulfillmentQueueLoadLevelingDemo.RunGeneratedAsync());

    [Scenario("Queue load leveling demo is importable through IServiceCollection")]
    [Fact]
    public Task Queue_Load_Leveling_Demo_Is_Importable_Through_IServiceCollection()
        => Given("an importing app service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddFulfillmentQueueLoadLevelingDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the service", provider =>
        {
            using (provider)
            {
                var service = provider.GetRequiredService<FulfillmentQueueLoadLevelingService>();
                return service.EnqueueAsync(new FulfillmentWorkItem("order-di", "central")).AsTask();
            }
        })
        .Then("the service accepts the queued work", result =>
        {
            ScenarioExpect.True(result.Accepted);
            ScenarioExpect.Equal("order-di", result.OrderId);
        })
        .AssertPassed();
}
