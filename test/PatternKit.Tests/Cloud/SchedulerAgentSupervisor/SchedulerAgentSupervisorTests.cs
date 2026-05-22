using PatternKit.Cloud.SchedulerAgentSupervisor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.SchedulerAgentSupervisor;

[Feature("Scheduler Agent Supervisor")]
public sealed class SchedulerAgentSupervisorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Scheduler dispatches due work to an agent")]
    [Fact]
    public Task Scheduler_Dispatches_Due_Work_To_An_Agent()
        => Given("a scheduler agent supervisor", () =>
        {
            var now = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
            var supervisor = SchedulerAgentSupervisor<OrderBatch, DispatchSummary>
                .Create("warehouse-supervisor")
                .Clock(() => now)
                .Agent("release-agent", ctx =>
                {
                    ctx.Events.Add($"released:{ctx.Work.BatchId}");
                    return new DispatchSummary(ctx.Work.BatchId, ctx.Attempt);
                })
                .Build()
                .Schedule("release-backlog", new OrderBatch("B-100"), now);
            return supervisor;
        })
        .When("due work is run", supervisor => supervisor.RunDue())
        .Then("the agent result captures dispatch details", results =>
        {
            var result = ScenarioExpect.Single(results);
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("warehouse-supervisor", result.SupervisorName);
            ScenarioExpect.Equal("release-backlog", result.JobName);
            ScenarioExpect.Equal("release-agent", result.AgentName);
            ScenarioExpect.Equal(1, result.Attempt);
            ScenarioExpect.Equal("B-100", result.Response!.BatchId);
            ScenarioExpect.Equal(["dispatch:release-agent:1", "released:B-100"], result.Events);
        })
        .AssertPassed();

    [Scenario("Scheduler only dispatches due jobs")]
    [Fact]
    public Task Scheduler_Only_Dispatches_Due_Jobs()
        => Given("a scheduler with due and future jobs", () =>
        {
            var now = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
            var supervisor = CreateSupervisor(now);
            supervisor.Schedule("due", new OrderBatch("B-1"), now);
            supervisor.Schedule("future", new OrderBatch("B-2"), now.AddMinutes(5));
            return new { Supervisor = supervisor, Now = now };
        })
        .When("due work is run", ctx => ctx.Supervisor.RunDue(ctx.Now))
        .Then("only due work is dispatched", results =>
        {
            var result = ScenarioExpect.Single(results);
            ScenarioExpect.Equal("due", result.JobName);
        })
        .AssertPassed();

    [Scenario("Supervisor reschedules retryable failures")]
    [Fact]
    public Task Supervisor_Reschedules_Retryable_Failures()
        => Given("a scheduler with retry policy", () =>
        {
            var now = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
            var attempts = 0;
            var supervisor = SchedulerAgentSupervisor<OrderBatch, DispatchSummary>
                .Create("warehouse-supervisor")
                .Supervision(SchedulerSupervisionPolicy<OrderBatch>.Create().MaxAttempts(3).RetryDelay(TimeSpan.FromSeconds(5)).Build())
                .Agent("release-agent", ctx =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("warehouse unavailable");
                    return new DispatchSummary(ctx.Work.BatchId, ctx.Attempt);
                })
                .Build()
                .Schedule("release-backlog", new OrderBatch("B-100"), now);
            return new { Supervisor = supervisor, Now = now };
        })
        .When("the first attempt fails", ctx => new { ctx.Supervisor, ctx.Now, First = ctx.Supervisor.RunDue(ctx.Now) })
        .Then("a retry is scheduled", ctx =>
        {
            var first = ScenarioExpect.Single(ctx.First);
            ScenarioExpect.True(first.Failed);
            ScenarioExpect.True(first.RetryScheduled);
            ScenarioExpect.False(first.Exhausted);
            ScenarioExpect.Equal(["release-backlog"], ctx.Supervisor.PendingJobs);
        })
        .And("the retry succeeds when due", ctx =>
        {
            var retry = ScenarioExpect.Single(ctx.Supervisor.RunDue(ctx.Now.AddSeconds(5)));
            ScenarioExpect.True(retry.Succeeded);
            ScenarioExpect.Equal(2, retry.Attempt);
        })
        .AssertPassed();

    [Scenario("Supervisor exhausts failures at retry limit")]
    [Fact]
    public Task Supervisor_Exhausts_Failures_At_Retry_Limit()
        => Given("a scheduler with one allowed attempt", () =>
        {
            var now = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
            var supervisor = SchedulerAgentSupervisor<OrderBatch, DispatchSummary>
                .Create("warehouse-supervisor")
                .Supervision(SchedulerSupervisionPolicy<OrderBatch>.Create()
                    .MaxAttempts(1)
                    .RetryWhen((_, ctx) => ctx.Work.BatchId != "never")
                    .Build())
                .Agent("release-agent", _ => throw new InvalidOperationException("failed"))
                .Build()
                .Schedule("release-backlog", new OrderBatch("never"), now);
            return new { Supervisor = supervisor, Now = now };
        })
        .When("the job fails", ctx => ctx.Supervisor.RunDue(ctx.Now))
        .Then("the result is exhausted and not retried", results =>
        {
            var result = ScenarioExpect.Single(results);
            ScenarioExpect.True(result.Failed);
            ScenarioExpect.False(result.RetryScheduled);
            ScenarioExpect.True(result.Exhausted);
        })
        .AssertPassed();

    [Scenario("Scheduler validates configuration")]
    [Fact]
    public Task Scheduler_Validates_Configuration()
        => Given("invalid scheduler inputs", () => new object())
        .Then("invalid configuration throws", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>.Create("").Agent("a", Complete).Build());
            ScenarioExpect.Throws<InvalidOperationException>(() => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>.Create().Build());
            ScenarioExpect.Throws<ArgumentException>(() => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>.Create().Agent("", Complete));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>.Create().Agent("a", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>.Create().Clock(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>.Create().Supervision(null!));
            ScenarioExpect.Throws<InvalidOperationException>(() => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>.Create().Agent("a", Complete).Agent("a", Complete));
            ScenarioExpect.Throws<ArgumentException>(() => CreateSupervisor(DateTimeOffset.UtcNow).Schedule("", new OrderBatch("B"), DateTimeOffset.UtcNow));
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateSupervisor(DateTimeOffset.UtcNow).Schedule("job", null!, DateTimeOffset.UtcNow));
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => SchedulerSupervisionPolicy<OrderBatch>.Create().MaxAttempts(0));
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => SchedulerSupervisionPolicy<OrderBatch>.Create().RetryDelay(TimeSpan.FromMilliseconds(-1)));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerSupervisionPolicy<OrderBatch>.Create().RetryWhen(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerAgentResult<DispatchSummary>.Success("s", "j", "a", 1, null!, []));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerAgentResult<DispatchSummary>.Success("s", "j", "a", 1, new("B", 1), null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerAgentResult<DispatchSummary>.Failure("s", "j", "a", 1, null!, [], false, true));
            ScenarioExpect.Throws<ArgumentNullException>(() => SchedulerAgentResult<DispatchSummary>.Failure("s", "j", "a", 1, new InvalidOperationException(), null!, false, true));
        })
        .AssertPassed();

    private static SchedulerAgentSupervisor<OrderBatch, DispatchSummary> CreateSupervisor(DateTimeOffset now)
        => SchedulerAgentSupervisor<OrderBatch, DispatchSummary>
            .Create("warehouse-supervisor")
            .Clock(() => now)
            .Agent("release-agent", Complete)
            .Build();

    private static DispatchSummary Complete(SchedulerAgentContext<OrderBatch> ctx) => new(ctx.Work.BatchId, ctx.Attempt);

    private sealed record OrderBatch(string BatchId);

    private sealed record DispatchSummary(string BatchId, int Attempt);
}
