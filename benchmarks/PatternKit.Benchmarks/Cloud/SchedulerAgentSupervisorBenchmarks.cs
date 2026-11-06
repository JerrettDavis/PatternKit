using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.SchedulerAgentSupervisor;
using PatternKit.Generators.SchedulerAgentSupervisor;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "SchedulerAgentSupervisor")]
public class SchedulerAgentSupervisorBenchmarks
{
    private static readonly DateTimeOffset Now = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
    private readonly SchedulerWork _work = new("sku-100", 12);

    [Benchmark(Baseline = true, Description = "Fluent: create supervisor")]
    [BenchmarkCategory("Fluent", "Construction")]
    public SchedulerAgentSupervisor<SchedulerWork, SchedulerSummary> Fluent_CreateSupervisor()
        => SchedulerAgentSupervisor<SchedulerWork, SchedulerSummary>
            .Create("benchmark-scheduler")
            .Supervision(SchedulerSupervisionPolicy<SchedulerWork>
                .Create()
                .MaxAttempts(3)
                .RetryDelay(TimeSpan.FromMilliseconds(250))
                .RetryWhen(static (_, context) => context.Attempt < 3)
                .Build())
            .Agent("release-agent", static context => new SchedulerSummary(context.Work.Sku, context.Work.Quantity))
            .Build();

    [Benchmark(Description = "Generated: create supervisor")]
    [BenchmarkCategory("Generated", "Construction")]
    public SchedulerAgentSupervisor<SchedulerWork, SchedulerSummary> Generated_CreateSupervisor()
        => GeneratedSchedulerAgentSupervisorBenchmark.Create();

    [Benchmark(Description = "Fluent: schedule and run due")]
    [BenchmarkCategory("Fluent", "Execution")]
    public IReadOnlyList<SchedulerAgentResult<SchedulerSummary>> Fluent_ScheduleAndRunDue()
        => Fluent_CreateSupervisor()
            .Schedule("replenish", _work, Now)
            .RunDue(Now);

    [Benchmark(Description = "Generated: schedule and run due")]
    [BenchmarkCategory("Generated", "Execution")]
    public IReadOnlyList<SchedulerAgentResult<SchedulerSummary>> Generated_ScheduleAndRunDue()
        => Generated_CreateSupervisor()
            .Schedule("replenish", _work, Now)
            .RunDue(Now);
}

public sealed record SchedulerWork(string Sku, int Quantity);

public sealed record SchedulerSummary(string Sku, int Released);

[GenerateSchedulerAgentSupervisor(
    typeof(SchedulerWork),
    typeof(SchedulerSummary),
    FactoryMethodName = "Create",
    SupervisorName = "benchmark-scheduler",
    MaxAttempts = 3,
    RetryDelayMilliseconds = 250)]
public static partial class GeneratedSchedulerAgentSupervisorBenchmark
{
    [SchedulerAgent("release-agent")]
    private static SchedulerSummary Release(SchedulerAgentContext<SchedulerWork> context)
        => new(context.Work.Sku, context.Work.Quantity);

    [SchedulerRetryWhen]
    private static bool Retry(Exception exception, SchedulerAgentContext<SchedulerWork> context)
        => context.Attempt < 3;
}
