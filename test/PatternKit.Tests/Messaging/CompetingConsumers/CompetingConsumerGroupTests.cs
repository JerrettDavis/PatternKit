using PatternKit.Messaging.CompetingConsumers;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Messaging.CompetingConsumers;

[Feature("Competing Consumers")]
public sealed class CompetingConsumerGroupTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record DistributionResult(
        CompetingConsumerResult<string> First,
        CompetingConsumerResult<string> Second,
        CompetingConsumerResult<string> Third);

    [Scenario("Competing consumers distribute messages across handlers")]
    [Fact]
    public Task Competing_Consumers_Distribute_Messages_Across_Handlers()
        => Given("a competing consumer group", () => CompetingConsumerGroup<string, string>.Create("fulfillment")
            .WithMaxConcurrentDeliveries(2)
            .AddConsumer("east", (message, _) => new ValueTask<string>($"east:{message}"))
            .AddConsumer("west", (message, _) => new ValueTask<string>($"west:{message}"))
            .Build())
        .When("messages are dispatched", DispatchThreeMessagesAsync)
        .Then("handlers compete for deliveries in round-robin order", results =>
        {
            ScenarioExpect.Equal("east", results.First.ConsumerName);
            ScenarioExpect.Equal("west", results.Second.ConsumerName);
            ScenarioExpect.Equal("east", results.Third.ConsumerName);
            ScenarioExpect.Equal("east:order-1", results.First.Value);
            ScenarioExpect.True(results.First.Accepted);
            ScenarioExpect.True(results.Second.Accepted);
            ScenarioExpect.True(results.Third.Accepted);
        })
        .AssertPassed();

    [Scenario("Competing consumers reject immediate dispatch when saturated")]
    [Fact]
    public Task Competing_Consumers_Reject_Immediate_Dispatch_When_Saturated()
        => Given("a saturated competing consumer group", () => CompetingConsumerGroup<string, string>.Create("fulfillment")
            .WithMaxConcurrentDeliveries(1)
            .AddConsumer("east", async (message, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return $"east:{message}";
            })
            .Build())
        .When("try-dispatch is called while the only delivery is active", group => RejectWhileBusyAsync(group))
        .Then("the second delivery is rejected", result =>
        {
            ScenarioExpect.False(result.Accepted);
            ScenarioExpect.True(result.Rejected);
            ScenarioExpect.Null(result.Value);
            ScenarioExpect.Equal(1, result.ActiveConsumers);
        })
        .AssertPassed();

    [Scenario("Competing consumers validate configuration")]
    [Fact]
    public Task Competing_Consumers_Validate_Configuration()
        => Given("invalid competing consumer inputs", () => true)
        .Then("invalid group names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => CompetingConsumerGroup<string, string>.Create("").AddConsumer("consumer", (message, _) => new ValueTask<string>(message)).Build()))
        .And("groups require a consumer", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => CompetingConsumerGroup<string, string>.Create().Build()))
        .And("invalid concurrency is rejected", _ =>
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => CompetingConsumerGroup<string, string>.Create().WithMaxConcurrentDeliveries(0).AddConsumer("consumer", (message, _) => new ValueTask<string>(message)).Build()))
        .And("invalid consumer names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => CompetingConsumerGroup<string, string>.Create().AddConsumer("", (message, _) => new ValueTask<string>(message)).Build()))
        .And("null handlers are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CompetingConsumerGroup<string, string>.Create().AddConsumer("consumer", null!)))
        .AssertPassed();

    private static async Task<CompetingConsumerResult<string>> RejectWhileBusyAsync(CompetingConsumerGroup<string, string> group)
    {
        using var cancellation = new CancellationTokenSource();
        var first = group.DispatchAsync("order-1", cancellation.Token).AsTask();
        ScenarioExpect.True(SpinWait.SpinUntil(() => group.ActiveDeliveries == 1, TimeSpan.FromSeconds(1)));
        try
        {
            return await group.TryDispatchAsync("order-2");
        }
        finally
        {
            cancellation.Cancel();
            await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() => first);
        }
    }

    private static async Task<DistributionResult> DispatchThreeMessagesAsync(CompetingConsumerGroup<string, string> group)
        => new(
            await group.DispatchAsync("order-1"),
            await group.DispatchAsync("order-2"),
            await group.DispatchAsync("order-3"));
}
