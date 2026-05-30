using BenchmarkDotNet.Attributes;
using PatternKit.Application.EventualConsistency;
using PatternKit.Examples.EventualConsistencyDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "EventualConsistencyMonitor")]
public class EventualConsistencyMonitorBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create eventual consistency monitor")]
    [BenchmarkCategory("Fluent", "Construction")]
    public EventualConsistencyMonitor<string> Fluent_CreateEventualConsistencyMonitor()
        => OrderProjectionConsistencyPolicies.CreateFluentMonitor();

    [Benchmark(Description = "Generated: create eventual consistency monitor")]
    [BenchmarkCategory("Generated", "Construction")]
    public EventualConsistencyMonitor<string> Generated_CreateEventualConsistencyMonitor()
        => GeneratedOrderProjectionConsistencyMonitor.CreateMonitor();

    [Benchmark(Description = "Fluent: record order projection consistency")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderProjectionConsistencySummary Fluent_RecordOrderProjectionConsistency()
        => OrderProjectionConsistencyDemo.RunFluent();

    [Benchmark(Description = "Generated: record order projection consistency")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderProjectionConsistencySummary Generated_RecordOrderProjectionConsistency()
        => OrderProjectionConsistencyDemo.RunGenerated();
}
