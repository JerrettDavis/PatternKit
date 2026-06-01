using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.DistributedLocks;
using PatternKit.Examples.DistributedLockDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "DistributedLockLease")]
public class DistributedLockBenchmarks
{
    private static readonly OrderAllocationRequest Request = new("ORDER-100", "allocator-a", 4);

    [Benchmark(Baseline = true, Description = "Fluent: create distributed lock")]
    [BenchmarkCategory("Fluent", "Construction")]
    public DistributedLock<string> Fluent_CreateDistributedLock()
        => OrderAllocationDistributedLocks.CreateFluent();

    [Benchmark(Description = "Generated: create distributed lock")]
    [BenchmarkCategory("Generated", "Construction")]
    public DistributedLock<string> Generated_CreateDistributedLock()
        => GeneratedOrderAllocationDistributedLock.Create();

    [Benchmark(Description = "Fluent: allocate order under lease")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderAllocationSummary Fluent_AllocateOrder()
    {
        var workflow = new OrderAllocationLockWorkflow(OrderAllocationDistributedLocks.CreateFluent());
        return workflow.Allocate(Request);
    }

    [Benchmark(Description = "Generated: allocate order under lease")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderAllocationSummary Generated_AllocateOrder()
    {
        var workflow = new OrderAllocationLockWorkflow(GeneratedOrderAllocationDistributedLock.Create());
        return workflow.Allocate(Request);
    }
}
