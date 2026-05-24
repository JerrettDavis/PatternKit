using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.QueueLoadLeveling;
using PatternKit.Examples.QueueLoadLevelingDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "QueueLoadLeveling")]
public class QueueLoadLevelingBenchmarks
{
    private static readonly FulfillmentWorkItem Work = new("order-100", "central");

    [Benchmark(Baseline = true, Description = "Fluent: create queue load leveling policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public QueueLoadLevelingPolicy<FulfillmentQueueResult> Fluent_CreatePolicy()
        => FulfillmentQueueLoadLevelingPolicies.CreateFluentPolicy();

    [Benchmark(Description = "Generated: create queue load leveling policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public QueueLoadLevelingPolicy<FulfillmentQueueResult> Generated_CreatePolicy()
        => GeneratedFulfillmentQueueLoadLevelingPolicy.CreatePolicy();

    [Benchmark(Description = "Fluent: enqueue fulfillment work")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<FulfillmentQueueResult> Fluent_EnqueueFulfillmentWork()
        => EnqueueWith(FulfillmentQueueLoadLevelingPolicies.CreateFluentPolicy());

    [Benchmark(Description = "Generated: enqueue fulfillment work")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<FulfillmentQueueResult> Generated_EnqueueFulfillmentWork()
        => EnqueueWith(GeneratedFulfillmentQueueLoadLevelingPolicy.CreatePolicy());

    private static ValueTask<FulfillmentQueueResult> EnqueueWith(QueueLoadLevelingPolicy<FulfillmentQueueResult> policy)
        => new FulfillmentQueueLoadLevelingService(new ScriptedFulfillmentWorker(), policy).EnqueueAsync(Work);
}
