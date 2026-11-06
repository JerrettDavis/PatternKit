using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.PipesAndFilters;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "PipesAndFilters")]
public class PipesAndFiltersBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create pipes and filters pipeline")]
    [BenchmarkCategory("Fluent", "Construction")]
    public PipesAndFiltersPipeline<FulfillmentPipelineContext> Fluent_CreatePipeline()
        => FulfillmentPipesAndFiltersPipelines.CreateFluentPipeline();

    [Benchmark(Description = "Generated: create pipes and filters pipeline")]
    [BenchmarkCategory("Generated", "Construction")]
    public PipesAndFiltersPipeline<FulfillmentPipelineContext> Generated_CreatePipeline()
        => FulfillmentPipesAndFiltersPipelines.CreateGeneratedPipeline();

    [Benchmark(Description = "Fluent: run fulfillment pipeline")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<FulfillmentPipesAndFiltersSummary> Fluent_RunFulfillmentPipeline()
        => FulfillmentPipesAndFiltersExample.RunFluentAsync();

    [Benchmark(Description = "Generated: run fulfillment pipeline")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<FulfillmentPipesAndFiltersSummary> Generated_RunFulfillmentPipeline()
        => FulfillmentPipesAndFiltersExample.RunGeneratedAsync();
}
