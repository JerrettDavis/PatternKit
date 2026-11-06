using BenchmarkDotNet.Attributes;
using PatternKit.Application.BoundedContexts;
using PatternKit.Examples.BoundedContextDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "BoundedContext")]
public class BoundedContextBenchmarks
{
    private static readonly FulfillmentBoundedContextDemo.CatalogProduct Product = new("SKU-1", 42m);

    private readonly BoundedContextDescriptor _fluent = FulfillmentBoundedContextDemo.CreateFluentDescriptor();

    private readonly BoundedContextDescriptor _generated = FulfillmentBoundedContextDemo.CreateGeneratedDescriptor();

    [Benchmark(Baseline = true, Description = "Fluent: create bounded context descriptor")]
    [BenchmarkCategory("Fluent", "Construction")]
    public BoundedContextDescriptor Fluent_CreateDescriptor()
        => FulfillmentBoundedContextDemo.CreateFluentDescriptor();

    [Benchmark(Description = "Generated: create bounded context descriptor")]
    [BenchmarkCategory("Generated", "Construction")]
    public BoundedContextDescriptor Generated_CreateDescriptor()
        => FulfillmentBoundedContextDemo.CreateGeneratedDescriptor();

    [Benchmark(Description = "Fluent: inspect bounded context boundary")]
    [BenchmarkCategory("Fluent", "Execution")]
    public int Fluent_InspectBoundary()
        => _fluent.Capabilities.Count + _fluent.Adapters.Count + FulfillmentBoundedContextDemo.Translate(Product).Sku.Length;

    [Benchmark(Description = "Generated: inspect bounded context boundary")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_InspectBoundary()
        => _generated.Capabilities.Count + _generated.Adapters.Count + FulfillmentBoundedContextDemo.Translate(Product).Sku.Length;
}
