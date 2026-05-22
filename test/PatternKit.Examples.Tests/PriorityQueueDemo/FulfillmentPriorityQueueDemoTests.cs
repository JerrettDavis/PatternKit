using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.PriorityQueueDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.PriorityQueueDemo;

[Feature("Fulfillment priority queue example")]
public sealed class FulfillmentPriorityQueueDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated priority queues schedule urgent fulfillment first")]
    [Fact]
    public Task Fluent_And_Generated_Priority_Queues_Schedule_Urgent_Fulfillment_First()
        => Given("fulfillment priority queue examples", () => new
        {
            Fluent = FulfillmentPriorityQueueDemoRunner.RunFluent(),
            Generated = FulfillmentPriorityQueueDemoRunner.RunGeneratedStatic()
        })
        .Then("both paths choose the enterprise order first", result =>
        {
            ScenarioExpect.Equal("order-enterprise", result.Fluent.FirstOrderId);
            ScenarioExpect.Equal("order-enterprise", result.Generated.FirstOrderId);
            ScenarioExpect.Equal(20, result.Generated.FirstPriority);
            ScenarioExpect.Equal("fulfillment-priority", result.Generated.QueueName);
        })
        .AssertPassed();

    [Scenario("Priority queue demo is importable through IServiceCollection")]
    [Fact]
    public Task Priority_Queue_Demo_Is_Importable_Through_IServiceCollection()
        => Given("an importing app service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddFulfillmentPriorityQueueDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the service", provider =>
        {
            using (provider)
            {
                var service = provider.GetRequiredService<FulfillmentPriorityQueueService>();
                return service.Schedule(
                    new FulfillmentPriorityWork("order-standard", "standard", false),
                    new FulfillmentPriorityWork("order-enterprise", "enterprise", false));
            }
        })
        .Then("the service schedules the highest priority work first", result =>
        {
            ScenarioExpect.Equal("order-enterprise", result.FirstOrderId);
            ScenarioExpect.Equal(1, result.RemainingCount);
        })
        .AssertPassed();

    [Scenario("Aggregate examples import priority queue demo")]
    [Fact]
    public Task Aggregate_Examples_Import_Priority_Queue_Demo()
        => Given("a PatternKit examples service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving the aggregate priority queue example", provider =>
        {
            using (provider)
                return provider.GetRequiredService<FulfillmentPriorityQueueExample>();
        })
        .Then("the aggregate example exposes the queue and service", example =>
        {
            ScenarioExpect.Equal("fulfillment-priority", example.Queue.Name);
            ScenarioExpect.NotNull(example.Service);
        })
        .AssertPassed();
}
