using BenchmarkDotNet.Attributes;
using PatternKit.Application.DomainEvents;
using PatternKit.Examples.DomainEventDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "DomainEvent")]
public class DomainEventBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create domain event dispatcher")]
    [BenchmarkCategory("Fluent", "Construction")]
    public DomainEventDispatcher<OrderDomainEvent> Fluent_CreateDispatcher()
        => OrderDomainEventPolicies.CreateFluentDispatcher(new OrderEventProjection(), new List<string>());

    [Benchmark(Description = "Generated: create domain event dispatcher")]
    [BenchmarkCategory("Generated", "Construction")]
    public IDomainEventDispatcher<OrderDomainEvent> Generated_CreateDispatcher()
        => GeneratedOrderDomainEvents.CreateDispatcher();

    [Benchmark(Description = "Fluent: dispatch order placed")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderDomainEventSummary> Fluent_DispatchOrderPlaced()
        => OrderDomainEventDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: dispatch order placed")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderDomainEventSummary> Generated_DispatchOrderPlaced()
        => OrderDomainEventDemo.RunGeneratedAsync();
}
