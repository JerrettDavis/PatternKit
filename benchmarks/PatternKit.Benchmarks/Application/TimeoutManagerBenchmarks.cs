using BenchmarkDotNet.Attributes;
using PatternKit.Application.Timeouts;
using PatternKit.Examples.TimeoutManagerDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "TimeoutManager")]
public class TimeoutManagerBenchmarks
{
    private static readonly OrderReservationRequest Request = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "REQ-100",
        TimeSpan.FromMinutes(15));

    private static readonly DateTimeOffset ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20);

    [Benchmark(Baseline = true, Description = "Fluent: create timeout manager")]
    [BenchmarkCategory("Fluent", "Construction")]
    public TimeoutManager<Guid> Fluent_CreateTimeoutManager()
        => OrderReservationTimeoutManagers.CreateFluent();

    [Benchmark(Description = "Generated: create timeout manager")]
    [BenchmarkCategory("Generated", "Construction")]
    public TimeoutManager<Guid> Generated_CreateTimeoutManager()
        => GeneratedOrderReservationTimeoutManager.CreateGenerated();

    [Benchmark(Description = "Fluent: expire order reservation timeout")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderReservationTimeoutSummary Fluent_ExpireOrderReservation()
        => OrderReservationTimeoutDemoRunner.RunFluent(Request, ExpiresAt);

    [Benchmark(Description = "Generated: expire order reservation timeout")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderReservationTimeoutSummary Generated_ExpireOrderReservation()
        => OrderReservationTimeoutDemoRunner.RunGeneratedStatic(Request, ExpiresAt);
}
