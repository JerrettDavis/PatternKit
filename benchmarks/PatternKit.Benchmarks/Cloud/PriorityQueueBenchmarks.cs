using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.PriorityQueue;
using PatternKit.Examples.PriorityQueueDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "PriorityQueue")]
public class PriorityQueueBenchmarks
{
    private static readonly FulfillmentPriorityWork Standard = new("order-standard", "standard", false);
    private static readonly FulfillmentPriorityWork Expedited = new("order-expedited", "standard", true);
    private static readonly FulfillmentPriorityWork Enterprise = new("order-enterprise", "enterprise", false);

    [Benchmark(Baseline = true, Description = "Fluent: create priority queue")]
    [BenchmarkCategory("Fluent", "Construction")]
    public PriorityQueuePolicy<FulfillmentPriorityWork, int> Fluent_CreateQueue()
        => FulfillmentPriorityQueues.CreateFluent();

    [Benchmark(Description = "Generated: create priority queue")]
    [BenchmarkCategory("Generated", "Construction")]
    public PriorityQueuePolicy<FulfillmentPriorityWork, int> Generated_CreateQueue()
        => GeneratedFulfillmentPriorityQueue.Create();

    [Benchmark(Description = "Fluent: enqueue and dequeue fulfillment work")]
    [BenchmarkCategory("Fluent", "Execution")]
    public FulfillmentPrioritySummary Fluent_ScheduleFulfillmentWork()
        => ScheduleWith(FulfillmentPriorityQueues.CreateFluent());

    [Benchmark(Description = "Generated: enqueue and dequeue fulfillment work")]
    [BenchmarkCategory("Generated", "Execution")]
    public FulfillmentPrioritySummary Generated_ScheduleFulfillmentWork()
        => ScheduleWith(GeneratedFulfillmentPriorityQueue.Create());

    private static FulfillmentPrioritySummary ScheduleWith(PriorityQueuePolicy<FulfillmentPriorityWork, int> queue)
        => new FulfillmentPriorityQueueService(queue).Schedule(Standard, Expedited, Enterprise);
}
