using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.Bulkhead;
using PatternKit.Examples.BulkheadDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "Bulkhead")]
public class BulkheadBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create bulkhead policy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public BulkheadPolicy<ShippingAllocation> Fluent_CreatePolicy()
        => ShippingBulkheadPolicies.CreateFluentPolicy();

    [Benchmark(Description = "Generated: create bulkhead policy")]
    [BenchmarkCategory("Generated", "Construction")]
    public BulkheadPolicy<ShippingAllocation> Generated_CreatePolicy()
        => GeneratedShippingBulkheadPolicy.CreateGeneratedPolicy();

    [Benchmark(Description = "Fluent: reserve shipping allocation")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<ShippingBulkheadSubmission> Fluent_ReserveAllocation()
    {
        var service = new ShippingBulkheadService(
            new ScriptedShippingAllocator(new ShippingAllocation("ORDER-100", "ground", true)),
            ShippingBulkheadPolicies.CreateFluentPolicy());

        return service.ReserveAsync("ORDER-100");
    }

    [Benchmark(Description = "Generated: reserve shipping allocation")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<ShippingBulkheadSubmission> Generated_ReserveAllocation()
    {
        var service = new ShippingBulkheadService(
            new ScriptedShippingAllocator(new ShippingAllocation("ORDER-100", "ground", true)),
            GeneratedShippingBulkheadPolicy.CreateGeneratedPolicy());

        return service.ReserveAsync("ORDER-100");
    }
}
