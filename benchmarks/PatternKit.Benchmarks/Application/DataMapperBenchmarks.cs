using BenchmarkDotNet.Attributes;
using PatternKit.Application.DataMapping;
using PatternKit.Examples.DataMapperDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "DataMapper")]
public class DataMapperBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create data mapper")]
    [BenchmarkCategory("Fluent", "Construction")]
    public DataMapper<OrderAggregate, OrderRow> Fluent_CreateMapper()
        => OrderDataMapperPolicies.CreateFluentMapper();

    [Benchmark(Description = "Generated: create data mapper")]
    [BenchmarkCategory("Generated", "Construction")]
    public IDataMapper<OrderAggregate, OrderRow> Generated_CreateMapper()
        => GeneratedOrderDataMapper.CreateMapper();

    [Benchmark(Description = "Fluent: map store and load order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderDataMapperSummary> Fluent_MapStoreAndLoadOrder()
        => OrderDataMapperDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: map store and load order")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderDataMapperSummary> Generated_MapStoreAndLoadOrder()
        => OrderDataMapperDemo.RunGeneratedAsync();
}
