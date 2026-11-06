using BenchmarkDotNet.Attributes;
using PatternKit.Application.TableDataGateway;
using PatternKit.Examples.TableDataGatewayDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "TableDataGateway")]
public class TableDataGatewayBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create table data gateway")]
    [BenchmarkCategory("Fluent", "Construction")]
    public InMemoryTableDataGateway<OrderTableRow, string> Fluent_CreateGateway()
        => OrderTableGatewayPolicies.CreateFluentGateway();

    [Benchmark(Description = "Generated: create table data gateway")]
    [BenchmarkCategory("Generated", "Construction")]
    public InMemoryTableDataGateway<OrderTableRow, string> Generated_CreateGateway()
        => GeneratedOrderTableGateway.CreateGateway();

    [Benchmark(Description = "Fluent: insert update query")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderTableGatewaySummary> Fluent_InsertUpdateQuery()
        => OrderTableDataGatewayDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: insert update query")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderTableGatewaySummary> Generated_InsertUpdateQuery()
        => OrderTableDataGatewayDemo.RunGeneratedAsync();
}
