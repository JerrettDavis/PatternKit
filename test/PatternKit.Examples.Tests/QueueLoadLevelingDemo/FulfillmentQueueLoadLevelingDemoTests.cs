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

    [Scenario("Queue load leveling service maps rejected work")]
    [Fact]
    public Task Queue_Load_Leveling_Service_Maps_Rejected_Work()
        => Given("a saturated fulfillment queue load leveling service", () => new RejectionFixture())
        .When("overflow work is submitted", fixture => fixture.RejectOverflowAsync())
        .Then("the service returns a rejected fulfillment result", result =>
        {
            ScenarioExpect.False(result.Accepted);
            ScenarioExpect.True(result.Rejected);
            ScenarioExpect.Equal("order-overflow", result.OrderId);
            ScenarioExpect.Equal("", result.Worker);
        })
        .AssertPassed();

    private sealed class RejectionFixture
    {
        private readonly BlockingFulfillmentWorker _worker = new();
        private readonly FulfillmentQueueLoadLevelingService _service;

        public RejectionFixture()
        {
            var policy = PatternKit.Cloud.QueueLoadLeveling.QueueLoadLevelingPolicy<FulfillmentQueueResult>
                .Create("fulfillment-queue")
                .WithMaxConcurrentWorkers(1)
                .WithMaxQueueLength(0)
                .Build();
            _service = new FulfillmentQueueLoadLevelingService(_worker, policy);
        }

        public async Task<FulfillmentQueueResult> RejectOverflowAsync()
        {
            var first = _service.EnqueueAsync(new FulfillmentWorkItem("order-active", "central")).AsTask();
            await _worker.Entered.Task;
            var rejected = await _service.EnqueueAsync(new FulfillmentWorkItem("order-overflow", "central"));
            _worker.Release.SetResult();
            await first;
            return rejected;
        }
    }

    private sealed class BlockingFulfillmentWorker : IFulfillmentWorker
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<FulfillmentQueueResult> ProcessAsync(FulfillmentWorkItem item, CancellationToken cancellationToken = default)
        {
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new FulfillmentQueueResult(item.OrderId, true, false, false, "worker-blocking");
        }
    }
}
