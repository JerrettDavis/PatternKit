using PatternKit.Messaging.Reliability.Backpressure;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability.Backpressure;

public sealed class BackpressurePolicyTests
{
    [Scenario("Backpressure rejects work when saturated")]
    [Fact]
    public async Task Backpressure_Rejects_Work_When_Saturated()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BackpressurePolicy<string>.Create("orders")
            .WithCapacity(1)
            .WithMode(BackpressureMode.Reject)
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
        var accepted = await first;

        ScenarioExpect.True(accepted.Accepted);
        ScenarioExpect.True(rejected.Rejected);
        ScenarioExpect.False(rejected.Accepted);
        ScenarioExpect.Equal("orders", policy.Name);
        ScenarioExpect.Equal(1, policy.Capacity);
        ScenarioExpect.Equal(BackpressureMode.Reject, policy.Mode);
    }

    [Scenario("Backpressure waits for capacity")]
    [Fact]
    public async Task Backpressure_Waits_For_Capacity()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BackpressurePolicy<int>.Create("wait")
            .WithCapacity(1)
            .WithMode(BackpressureMode.Wait)
            .WithWaitTimeout(TimeSpan.FromSeconds(2))
            .Build();

        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return 1;
        });
        await entered.Task;

        var second = policy.ExecuteAsync(static _ => new ValueTask<int>(2));
        release.SetResult();

        _ = await first;
        var waited = await second;

        ScenarioExpect.True(waited.Accepted);
        ScenarioExpect.True(waited.Waited);
        ScenarioExpect.Equal(2, waited.Value);
        ScenarioExpect.Equal(0, policy.ActiveCount);
    }

    [Scenario("Synchronous backpressure reports explicit saturation policies")]
    [Theory]
    [InlineData(BackpressureMode.Reject, false, false, false, true)]
    [InlineData(BackpressureMode.DropNewest, true, false, false, false)]
    [InlineData(BackpressureMode.Shed, false, true, false, false)]
    [InlineData(BackpressureMode.Observe, false, false, true, false)]
    [InlineData(BackpressureMode.DropOldest, true, false, false, false)]
    public async Task Synchronous_Backpressure_Reports_Explicit_Saturation_Policies(
        BackpressureMode mode,
        bool dropped,
        bool shed,
        bool observed,
        bool rejected)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BackpressurePolicy<string>.Create("sync-policy")
            .WithCapacity(1)
            .WithMode(mode)
            .Build();

        var first = Task.Run(() => policy.Execute(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
            return "active";
        }));
        await entered.Task;

        var saturated = policy.Execute(static () => "fallback");
        release.SetResult();
        var completed = await first;

        ScenarioExpect.True(completed.Accepted);
        ScenarioExpect.Equal(dropped, saturated.Dropped);
        ScenarioExpect.Equal(shed, saturated.Shed);
        ScenarioExpect.Equal(observed, saturated.Observed);
        ScenarioExpect.Equal(rejected, saturated.Rejected);
        ScenarioExpect.Equal(mode is BackpressureMode.DropNewest or BackpressureMode.DropOldest ? 1 : 0, policy.DroppedCount);
    }

    [Scenario("Synchronous backpressure wait can time out")]
    [Fact]
    public async Task Synchronous_Backpressure_Wait_Can_Time_Out()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BackpressurePolicy<string>.Create("sync-timeout")
            .WithCapacity(1)
            .WithMode(BackpressureMode.Wait)
            .WithWaitTimeout(TimeSpan.FromMilliseconds(10))
            .Build();

        var first = Task.Run(() => policy.Execute(() =>
        {
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
            return "active";
        }));
        await entered.Task;

        var timedOut = policy.Execute(static () => "late");
        release.SetResult();
        _ = await first;

        ScenarioExpect.True(timedOut.Rejected);
        ScenarioExpect.False(timedOut.Accepted);
        ScenarioExpect.Equal(0, policy.ActiveCount);
    }

    [Scenario("Backpressure reports explicit saturation policies")]
    [Theory]
    [InlineData(BackpressureMode.DropNewest, true, false, false)]
    [InlineData(BackpressureMode.Shed, false, true, false)]
    [InlineData(BackpressureMode.Observe, false, false, true)]
    [InlineData(BackpressureMode.DropOldest, true, false, false)]
    public async Task Backpressure_Reports_Explicit_Saturation_Policies(
        BackpressureMode mode,
        bool dropped,
        bool shed,
        bool observed)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var policy = BackpressurePolicy<string>.Create("policy")
            .WithCapacity(1)
            .WithMode(mode)
            .Build();

        var first = policy.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
            return "active";
        });
        await entered.Task;

        var saturated = await policy.ExecuteAsync(static _ => new ValueTask<string>("fallback"));
        release.SetResult();
        _ = await first;

        ScenarioExpect.Equal(dropped, saturated.Dropped);
        ScenarioExpect.Equal(shed, saturated.Shed);
        ScenarioExpect.Equal(observed, saturated.Observed);
        ScenarioExpect.Equal(mode is BackpressureMode.DropNewest or BackpressureMode.DropOldest ? 1 : 0, policy.DroppedCount);
    }

    [Scenario("Backpressure validates configuration and callbacks")]
    [Fact]
    public async Task Backpressure_Validates_Configuration_And_Callbacks()
    {
        var policy = BackpressurePolicy<string>.Create().Build();

        ScenarioExpect.Throws<ArgumentException>(() => BackpressurePolicy<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => BackpressurePolicy<string>.Create().WithCapacity(0).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => BackpressurePolicy<string>.Create().WithMode((BackpressureMode)99).Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => BackpressurePolicy<string>.Create().WithWaitTimeout(TimeSpan.FromMilliseconds(-1)).Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => policy.Execute(null!));
        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => policy.ExecuteAsync(null!).AsTask());
    }
}
