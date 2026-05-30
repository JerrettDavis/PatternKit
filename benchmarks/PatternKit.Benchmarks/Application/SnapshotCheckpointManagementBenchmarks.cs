using BenchmarkDotNet.Attributes;
using PatternKit.Application.SnapshotCheckpoints;
using PatternKit.Examples.SnapshotCheckpointDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "SnapshotCheckpointManagement")]
public class SnapshotCheckpointManagementBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create snapshot checkpoint manager")]
    [BenchmarkCategory("Fluent", "Construction")]
    public SnapshotCheckpointManager<string, OrderReplaySnapshot> Fluent_CreateSnapshotCheckpointManager()
        => OrderReplaySnapshotCheckpointPolicies.CreateFluentManager();

    [Benchmark(Description = "Generated: create snapshot checkpoint manager")]
    [BenchmarkCategory("Generated", "Construction")]
    public SnapshotCheckpointManager<string, OrderReplaySnapshot> Generated_CreateSnapshotCheckpointManager()
        => GeneratedOrderReplayCheckpoints.CreateManager();

    [Benchmark(Description = "Fluent: replay order with snapshot checkpoint")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderReplaySummary Fluent_ReplayOrderWithSnapshotCheckpoint()
        => OrderReplaySnapshotCheckpointDemo.RunFluentAsync().AsTask().GetAwaiter().GetResult();

    [Benchmark(Description = "Generated: replay order with snapshot checkpoint")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderReplaySummary Generated_ReplayOrderWithSnapshotCheckpoint()
        => OrderReplaySnapshotCheckpointDemo.RunGeneratedAsync().AsTask().GetAwaiter().GetResult();
}
