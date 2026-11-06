using BenchmarkDotNet.Attributes;
using PatternKit.Application.Aggregates;
using PatternKit.Examples.AggregateRootDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "AggregateRoot")]
public class AggregateRootBenchmarks
{
    private readonly AggregateCommandHandler<OrderAggregateRootDemo.OrderAggregate, OrderAggregateRootDemo.OrderCommand, OrderAggregateRootDemo.IOrderEvent> _fluent =
        OrderAggregateRootDemo.CreateFluentHandler();

    private readonly AggregateCommandHandler<OrderAggregateRootDemo.OrderAggregate, OrderAggregateRootDemo.OrderCommand, OrderAggregateRootDemo.IOrderEvent> _generated =
        OrderAggregateRootDemo.CreateGeneratedHandler();

    [Benchmark(Baseline = true, Description = "Fluent: create aggregate command handler")]
    [BenchmarkCategory("Fluent", "Construction")]
    public AggregateCommandHandler<OrderAggregateRootDemo.OrderAggregate, OrderAggregateRootDemo.OrderCommand, OrderAggregateRootDemo.IOrderEvent> Fluent_CreateHandler()
        => OrderAggregateRootDemo.CreateFluentHandler();

    [Benchmark(Description = "Generated: create aggregate command handler")]
    [BenchmarkCategory("Generated", "Construction")]
    public AggregateCommandHandler<OrderAggregateRootDemo.OrderAggregate, OrderAggregateRootDemo.OrderCommand, OrderAggregateRootDemo.IOrderEvent> Generated_CreateHandler()
        => OrderAggregateRootDemo.CreateGeneratedHandler();

    [Benchmark(Description = "Fluent: execute aggregate workflow")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderAggregateRootDemo.OrderSummary Fluent_ExecuteWorkflow()
        => OrderAggregateRootDemo.ExecuteOrder(_fluent);

    [Benchmark(Description = "Generated: execute aggregate workflow")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderAggregateRootDemo.OrderSummary Generated_ExecuteWorkflow()
        => OrderAggregateRootDemo.ExecuteOrder(_generated);
}
