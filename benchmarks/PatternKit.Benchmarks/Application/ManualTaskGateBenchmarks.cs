using BenchmarkDotNet.Attributes;
using PatternKit.Application.ManualTaskGates;
using PatternKit.Examples.ManualTaskGateDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "ManualTaskGate")]
public class ManualTaskGateBenchmarks
{
    private static readonly OrderApprovalRequest Request = new(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        "REQ-200",
        1250.00m,
        "checkout-api");

    [Benchmark(Baseline = true, Description = "Fluent: create manual task gate")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ManualTaskGate<Guid> Fluent_CreateManualTaskGate()
        => OrderApprovalManualTaskGates.CreateFluent();

    [Benchmark(Description = "Generated: create manual task gate")]
    [BenchmarkCategory("Generated", "Construction")]
    public ManualTaskGate<Guid> Generated_CreateManualTaskGate()
        => GeneratedOrderApprovalManualTaskGate.CreateGenerated();

    [Benchmark(Description = "Fluent: approve order manual task")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderApprovalSummary Fluent_ApproveOrder()
        => OrderApprovalManualTaskGateDemoRunner.RunFluent(Request);

    [Benchmark(Description = "Generated: approve order manual task")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderApprovalSummary Generated_ApproveOrder()
        => OrderApprovalManualTaskGateDemoRunner.RunGeneratedStatic(Request);
}
