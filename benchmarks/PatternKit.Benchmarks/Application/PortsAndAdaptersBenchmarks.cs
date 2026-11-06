using BenchmarkDotNet.Attributes;
using PatternKit.Application.PortsAndAdapters;
using PatternKit.Examples.PortsAndAdaptersDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "PortsAndAdapters")]
public class PortsAndAdaptersBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create ports and adapters pipeline")]
    [BenchmarkCategory("Fluent", "Construction")]
    public PortsAndAdaptersPipeline<OrderEntryHttpRequest, PlaceOrderCommand, PlaceOrderResult, OrderEntryHttpResponse> Fluent_CreatePipeline()
        => OrderEntryPortsAndAdaptersPolicies.CreateFluent(new InMemoryOrderEntryApplicationPort());

    [Benchmark(Description = "Generated: create ports and adapters pipeline")]
    [BenchmarkCategory("Generated", "Construction")]
    public PortsAndAdaptersPipeline<OrderEntryHttpRequest, PlaceOrderCommand, PlaceOrderResult, OrderEntryHttpResponse> Generated_CreatePipeline()
        => GeneratedOrderEntryPortsAndAdapters.CreateGenerated();

    [Benchmark(Description = "Fluent: execute ports and adapters pipeline")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderEntryHttpResponse> Fluent_Execute()
        => OrderEntryPortsAndAdaptersPolicies.CreateFluent(new InMemoryOrderEntryApplicationPort())
            .ExecuteAsync(new("order-100", "buyer@example.com", 42m));

    [Benchmark(Description = "Generated: execute ports and adapters pipeline")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderEntryHttpResponse> Generated_Execute()
    {
        GeneratedOrderEntryPortsAndAdapters.ApplicationPort = new InMemoryOrderEntryApplicationPort();
        return GeneratedOrderEntryPortsAndAdapters.CreateGenerated()
            .ExecuteAsync(new("order-100", "buyer@example.com", 42m));
    }
}
