using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.CompetingConsumers;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "CompetingConsumers")]
public class CompetingConsumersBenchmarks
{
    private static readonly FulfillmentConsumerWork Work = new("order-200", "east");

    [Benchmark(Baseline = true, Description = "Fluent: create competing consumer group")]
    [BenchmarkCategory("Fluent", "Construction")]
    public CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult> Fluent_CreateConsumerGroup()
        => FulfillmentCompetingConsumerGroups.CreateFluentGroup();

    [Benchmark(Description = "Generated: create competing consumer group")]
    [BenchmarkCategory("Generated", "Construction")]
    public CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult> Generated_CreateConsumerGroup()
        => FulfillmentCompetingConsumerGroups.CreateGeneratedGroup();

    [Benchmark(Description = "Fluent: dispatch fulfillment work")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<CompetingConsumerResult<FulfillmentConsumerResult>> Fluent_DispatchFulfillmentWork()
        => new FulfillmentCompetingConsumerService(FulfillmentCompetingConsumerGroups.CreateFluentGroup()).DispatchAsync(Work);

    [Benchmark(Description = "Generated: dispatch fulfillment work")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<CompetingConsumerResult<FulfillmentConsumerResult>> Generated_DispatchFulfillmentWork()
        => new FulfillmentCompetingConsumerService(FulfillmentCompetingConsumerGroups.CreateGeneratedGroup()).DispatchAsync(Work);
}
