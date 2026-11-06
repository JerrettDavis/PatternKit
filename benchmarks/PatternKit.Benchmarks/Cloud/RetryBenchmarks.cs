using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.Retry;
using PatternKit.Examples.RetryDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "Retry")]
public class RetryBenchmarks
{
    private static readonly InventoryResponse Transient = new("SKU-42", 0, 503);
    private static readonly InventoryResponse Available = new("SKU-42", 12, 200);

    [Benchmark(Baseline = true, Description = "Fluent: create retry policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public RetryPolicy<InventoryResponse> Fluent_CreatePolicy()
        => InventoryRetryPolicies.CreateFluentPolicy();

    [Benchmark(Description = "Generated: create retry policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public RetryPolicy<InventoryResponse> Generated_CreatePolicy()
        => GeneratedInventoryRetryPolicy.CreateGeneratedPolicy();

    [Benchmark(Description = "Fluent: retry transient result")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<InventoryLookupResult> Fluent_RetryTransientResult()
    {
        var service = new InventoryLookupService(
            new ScriptedInventoryClient(Transient, Available),
            InventoryRetryPolicies.CreateFluentPolicy());

        return service.CheckAsync("SKU-42");
    }

    [Benchmark(Description = "Generated: retry transient result")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<InventoryLookupResult> Generated_RetryTransientResult()
    {
        var service = new InventoryLookupService(
            new ScriptedInventoryClient(Transient, Available),
            GeneratedInventoryRetryPolicy.CreateGeneratedPolicy());

        return service.CheckAsync("SKU-42");
    }
}
