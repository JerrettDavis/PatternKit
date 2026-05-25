using BenchmarkDotNet.Attributes;
using PatternKit.Application.EventSourcing;
using PatternKit.Examples.EventSourcingDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "EventSourcing")]
public class EventSourcingBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create event store")]
    [BenchmarkCategory("Fluent", "Construction")]
    public IEventStore<OrderEvent, string> Fluent_CreateStore()
        => OrderEventSourcingPolicies.CreateFluentStore();

    [Benchmark(Description = "Generated: create event store")]
    [BenchmarkCategory("Generated", "Construction")]
    public IEventStore<OrderEvent, string> Generated_CreateStore()
        => GeneratedOrderEventStore.CreateStore();

    [Benchmark(Description = "Fluent: place and pay order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderEventSourcingSummary> Fluent_PlaceAndPayOrder()
        => OrderEventSourcingDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: place and pay order")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderEventSourcingSummary> Generated_PlaceAndPayOrder()
        => OrderEventSourcingDemo.RunGeneratedAsync();
}
