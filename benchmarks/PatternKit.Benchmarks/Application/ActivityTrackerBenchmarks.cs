using BenchmarkDotNet.Attributes;
using PatternKit.Application.ActivityTracking;
using PatternKit.Examples.ActivityTrackingDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "ActivityTracker")]
public class ActivityTrackerBenchmarks
{
    private static readonly DashboardLoadRequest Request = new("REQ-100", ["orders", "inventory", "pricing"]);

    [Benchmark(Baseline = true, Description = "Fluent: create activity tracker")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ActivityTracker Fluent_CreateActivityTracker()
        => DashboardActivityTrackers.CreateFluent();

    [Benchmark(Description = "Generated: create activity tracker")]
    [BenchmarkCategory("Generated", "Construction")]
    public ActivityTracker Generated_CreateActivityTracker()
        => GeneratedDashboardActivityTracker.CreateGenerated();

    [Benchmark(Description = "Fluent: track dashboard loading")]
    [BenchmarkCategory("Fluent", "Execution")]
    public DashboardLoadSummary Fluent_TrackDashboardLoading()
        => DashboardActivityTrackerDemoRunner.RunFluent(Request);

    [Benchmark(Description = "Generated: track dashboard loading")]
    [BenchmarkCategory("Generated", "Execution")]
    public DashboardLoadSummary Generated_TrackDashboardLoading()
        => DashboardActivityTrackerDemoRunner.RunGeneratedStatic(Request);
}
