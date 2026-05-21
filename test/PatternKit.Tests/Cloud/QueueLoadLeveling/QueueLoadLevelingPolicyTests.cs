using PatternKit.Cloud.QueueLoadLeveling;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.QueueLoadLeveling;

[Feature("Queue-Based Load Leveling")]
public sealed class QueueLoadLevelingPolicyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Queue load leveling accepts immediate work")]
    [Fact]
    public Task Queue_Load_Leveling_Accepts_Immediate_Work()
        => Given("a queue load leveling policy", () => QueueLoadLevelingPolicy<string>.Create("fulfillment")
            .WithMaxConcurrentWorkers(1)
            .WithMaxQueueLength(1)
            .WithQueueTimeout(TimeSpan.FromMilliseconds(50))
            .Build())
        .When("executing work", policy => policy.Execute(() => "accepted"))
        .Then("the result is accepted without queuing", result =>
        {
            ScenarioExpect.True(result.Accepted);
            ScenarioExpect.False(result.Queued);
            ScenarioExpect.Equal("accepted", result.Value);
        })
        .AssertPassed();

    [Scenario("Queue load leveling queues bursts behind workers")]
    [Fact]
    public Task Queue_Load_Leveling_Queues_Bursts_Behind_Workers()
        => Given("a busy queue load leveling policy", () => QueueLoadLevelingPolicy<string>.Create("fulfillment")
            .WithMaxConcurrentWorkers(1)
            .WithMaxQueueLength(1)
            .WithQueueTimeout(TimeSpan.FromMilliseconds(500))
            .Build())
        .When("two work items run concurrently", policy => QueueBehindBusyWorkerAsync(policy))
        .Then("the second result was queued and accepted", result =>
        {
            ScenarioExpect.True(result.Accepted);
            ScenarioExpect.True(result.Queued);
            ScenarioExpect.Equal("second", result.Value);
        })
        .AssertPassed();

    [Scenario("Queue load leveling rejects overflow")]
    [Fact]
    public Task Queue_Load_Leveling_Rejects_Overflow()
        => Given("a saturated queue load leveling policy", () => QueueLoadLevelingPolicy<string>.Create("fulfillment")
            .WithMaxConcurrentWorkers(1)
            .WithMaxQueueLength(0)
            .Build())
        .When("a second work item arrives while the worker is busy", policy => RejectOverflowAsync(policy))
        .Then("the overflow work is rejected", result =>
        {
            ScenarioExpect.False(result.Accepted);
            ScenarioExpect.True(result.Rejected);
        })
        .AssertPassed();

    [Scenario("Queue load leveling validates configuration")]
    [Fact]
    public Task Queue_Load_Leveling_Validates_Configuration()
        => Given("invalid queue load leveling inputs", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => QueueLoadLevelingPolicy<string>.Create("").Build()))
        .And("invalid worker counts are rejected", _ =>
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => QueueLoadLevelingPolicy<string>.Create().WithMaxConcurrentWorkers(0).Build()))
        .And("invalid queue lengths are rejected", _ =>
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => QueueLoadLevelingPolicy<string>.Create().WithMaxQueueLength(-1).Build()))
        .And("invalid timeouts are rejected", _ =>
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => QueueLoadLevelingPolicy<string>.Create().WithQueueTimeout(TimeSpan.FromMilliseconds(-1)).Build()))
        .And("null operations are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => QueueLoadLevelingPolicy<string>.Create().Build().Execute(null!)))
        .AssertPassed();

    [Scenario("Queue load leveling times out queued work")]
    [Fact]
    public Task Queue_Load_Leveling_Times_Out_Queued_Work()
        => Given("a queue load leveling policy with a short queue timeout", () => QueueLoadLevelingPolicy<string>.Create("fulfillment")
            .WithMaxConcurrentWorkers(1)
            .WithMaxQueueLength(1)
            .WithQueueTimeout(TimeSpan.FromMilliseconds(1))
            .Build())
        .When("queued work cannot acquire a worker before the timeout", policy => TimeoutQueuedWorkAsync(policy))
        .Then("the queued work reports a timeout", result =>
        {
            ScenarioExpect.False(result.Accepted);
            ScenarioExpect.False(result.Rejected);
            ScenarioExpect.True(result.TimedOut);
            ScenarioExpect.True(result.Queued);
        })
        .AssertPassed();

    [Scenario("Queue load leveling honors cancellation before enqueueing")]
    [Fact]
    public Task Queue_Load_Leveling_Honors_Cancellation_Before_Enqueueing()
        => Given("a queue load leveling policy and canceled token", () => new
        {
            Policy = QueueLoadLevelingPolicy<string>.Create("fulfillment").Build(),
            Cancellation = new CancellationToken(canceled: true)
        })
        .Then("cancellation is propagated", state =>
            ScenarioExpect.Throws<OperationCanceledException>(() => state.Policy
                .ExecuteAsync(_ => new ValueTask<string>("canceled"), state.Cancellation)
                .AsTask()
                .GetAwaiter()
                .GetResult()))
        .AssertPassed();

    private static async Task<QueueLoadLevelingResult<string>> QueueBehindBusyWorkerAsync(QueueLoadLevelingPolicy<string> policy)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return "first";
        }).AsTask();
        await entered.Task;
        var second = policy.ExecuteAsync(_ => new ValueTask<string>("second")).AsTask();
        release.SetResult();
        await first;
        return await second;
    }

    private static async Task<QueueLoadLevelingResult<string>> RejectOverflowAsync(QueueLoadLevelingPolicy<string> policy)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return "first";
        }).AsTask();
        await entered.Task;
        var rejected = await policy.ExecuteAsync(_ => new ValueTask<string>("second"));
        release.SetResult();
        await first;
        return rejected;
    }

    private static async Task<QueueLoadLevelingResult<string>> TimeoutQueuedWorkAsync(QueueLoadLevelingPolicy<string> policy)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return "first";
        }).AsTask();
        await entered.Task;
        var timedOut = await policy.ExecuteAsync(_ => new ValueTask<string>("second"));
        release.SetResult();
        await first;
        return timedOut;
    }
}
