using PatternKit.Cloud.Bulkhead;
using TinyBDD;

namespace PatternKit.Tests.Cloud.Bulkhead;

public sealed class BulkheadPolicyTests
{
    [Scenario("Bulkhead rejects calls when concurrency and queue are full")]
    [Fact]
    public async Task Bulkhead_Rejects_Calls_When_Concurrency_And_Queue_Are_Full()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BulkheadPolicy<string>.Create("fulfillment")
            .WithMaxConcurrency(1)
            .WithMaxQueueLength(0)
            .Build();

        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return "accepted";
        });
        await entered.Task;

        var rejected = await policy.ExecuteAsync(static _ => new ValueTask<string>("overflow"));
        release.SetResult();
        var completed = await first;

        ScenarioExpect.True(completed.Succeeded);
        ScenarioExpect.True(rejected.Rejected);
        ScenarioExpect.False(rejected.TimedOut);
    }

    [Scenario("Bulkhead queues calls inside the configured queue length")]
    [Fact]
    public async Task Bulkhead_Queues_Calls_Inside_Configured_Queue_Length()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BulkheadPolicy<int>.Create("priced-work")
            .WithMaxConcurrency(1)
            .WithMaxQueueLength(1)
            .WithQueueTimeout(TimeSpan.FromSeconds(5))
            .Build();

        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return 1;
        });
        await entered.Task;

        var second = policy.ExecuteAsync(static _ => new ValueTask<int>(2));
        ScenarioExpect.Equal(1, policy.QueuedCount);
        release.SetResult();

        var firstResult = await first;
        var secondResult = await second;

        ScenarioExpect.True(firstResult.Succeeded);
        ScenarioExpect.True(secondResult.Succeeded);
        ScenarioExpect.True(secondResult.Queued);
        ScenarioExpect.Equal(2, secondResult.Value);
        ScenarioExpect.Equal(0, policy.QueuedCount);
    }

    [Scenario("Bulkhead times out queued calls")]
    [Fact]
    public async Task Bulkhead_Times_Out_Queued_Calls()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BulkheadPolicy<string>.Create("timeout")
            .WithMaxConcurrency(1)
            .WithMaxQueueLength(1)
            .WithQueueTimeout(TimeSpan.FromMilliseconds(10))
            .Build();

        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return "done";
        });
        await entered.Task;

        var timedOut = await policy.ExecuteAsync(static _ => new ValueTask<string>("late"));
        release.SetResult();
        _ = await first;

        ScenarioExpect.False(timedOut.Succeeded);
        ScenarioExpect.True(timedOut.TimedOut);
        ScenarioExpect.False(timedOut.Rejected);
        ScenarioExpect.Equal(0, policy.QueuedCount);
    }

    [Scenario("Bulkhead releases slots when operations throw")]
    [Fact]
    public void Bulkhead_Releases_Slots_When_Operations_Throw()
    {
        var policy = BulkheadPolicy<string>.Create("exceptions")
            .WithMaxConcurrency(1)
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => policy.Execute(() => throw new InvalidOperationException("fatal")));
        var result = policy.Execute(static () => "recovered");

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("recovered", result.Value);
        ScenarioExpect.Equal(1, policy.AvailableSlots);
    }

    [Scenario("Synchronous bulkhead rejects calls when concurrency and queue are full")]
    [Fact]
    public async Task Synchronous_Bulkhead_Rejects_Calls_When_Concurrency_And_Queue_Are_Full()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BulkheadPolicy<string>.Create("sync-reject")
            .WithMaxConcurrency(1)
            .WithMaxQueueLength(0)
            .Build();

        var first = Task.Run(() => policy.Execute(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
            return "accepted";
        }));
        await entered.Task;

        var rejected = policy.Execute(static () => "overflow");
        release.SetResult();
        var completed = await first;

        ScenarioExpect.Equal("sync-reject", policy.Name);
        ScenarioExpect.Equal(1, policy.MaxConcurrency);
        ScenarioExpect.Equal(0, policy.MaxQueueLength);
        ScenarioExpect.Equal(TimeSpan.Zero, policy.QueueTimeout);
        ScenarioExpect.True(completed.Succeeded);
        ScenarioExpect.True(rejected.Rejected);
        ScenarioExpect.False(rejected.Succeeded);
        ScenarioExpect.False(rejected.TimedOut);
        ScenarioExpect.False(rejected.Queued);
        ScenarioExpect.Equal(0, rejected.AvailableSlots);
    }

    [Scenario("Synchronous bulkhead queues calls inside the configured queue length")]
    [Fact]
    public async Task Synchronous_Bulkhead_Queues_Calls_Inside_Configured_Queue_Length()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BulkheadPolicy<int>.Create("sync-queue")
            .WithMaxConcurrency(1)
            .WithMaxQueueLength(1)
            .WithQueueTimeout(TimeSpan.FromSeconds(5))
            .Build();

        var first = Task.Run(() => policy.Execute(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
            return 1;
        }));
        await entered.Task;

        var second = Task.Run(() => policy.Execute(static () => 2));
        SpinWait.SpinUntil(() => policy.QueuedCount == 1, TimeSpan.FromSeconds(2));
        release.SetResult();

        var firstResult = await first;
        var secondResult = await second;

        ScenarioExpect.True(firstResult.Succeeded);
        ScenarioExpect.True(secondResult.Succeeded);
        ScenarioExpect.True(secondResult.Queued);
        ScenarioExpect.Equal(2, secondResult.Value);
        ScenarioExpect.Equal(0, secondResult.AvailableSlots);
        ScenarioExpect.Equal(0, policy.QueuedCount);
    }

    [Scenario("Synchronous bulkhead times out queued calls")]
    [Fact]
    public async Task Synchronous_Bulkhead_Times_Out_Queued_Calls()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BulkheadPolicy<string>.Create("sync-timeout")
            .WithMaxConcurrency(1)
            .WithMaxQueueLength(1)
            .WithQueueTimeout(TimeSpan.FromMilliseconds(10))
            .Build();

        var first = Task.Run(() => policy.Execute(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
            return "accepted";
        }));
        await entered.Task;

        var timedOut = policy.Execute(static () => "late");
        release.SetResult();
        _ = await first;

        ScenarioExpect.False(timedOut.Succeeded);
        ScenarioExpect.False(timedOut.Rejected);
        ScenarioExpect.True(timedOut.TimedOut);
        ScenarioExpect.True(timedOut.Queued);
        ScenarioExpect.Equal(0, timedOut.AvailableSlots);
        ScenarioExpect.Equal(0, policy.QueuedCount);
    }

    [Scenario("Async bulkhead preserves cancellation")]
    [Fact]
    public async Task AsyncBulkhead_Preserves_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var policy = BulkheadPolicy<string>.Create("cancel").Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync(static _ => new ValueTask<string>("never"), cts.Token).AsTask());
    }

    [Scenario("Bulkhead rejects invalid configuration")]
    [Fact]
    public async Task Bulkhead_Rejects_Invalid_Configuration()
    {
        var policy = BulkheadPolicy<string>.Create("nulls").Build();

        ScenarioExpect.Throws<ArgumentException>(() => BulkheadPolicy<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => BulkheadPolicy<string>.Create().WithMaxConcurrency(0).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => BulkheadPolicy<string>.Create().WithMaxQueueLength(-1).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => BulkheadPolicy<string>.Create().WithQueueTimeout(TimeSpan.FromMilliseconds(-1)).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => policy.Execute(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => policy.ExecuteAsync(null!).AsTask());
    }
}
