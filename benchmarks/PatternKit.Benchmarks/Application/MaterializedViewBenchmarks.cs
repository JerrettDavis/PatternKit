using BenchmarkDotNet.Attributes;
using PatternKit.Application.MaterializedViews;
using PatternKit.Examples.MaterializedViewDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "MaterializedView")]
public class MaterializedViewBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create materialized view")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MaterializedView<OrderReadModel, OrderReadModelEvent> Fluent_CreateView()
        => OrderMaterializedViewPolicies.CreateFluentView();

    [Benchmark(Description = "Generated: create materialized view")]
    [BenchmarkCategory("Generated", "Construction")]
    public IMaterializedView<OrderReadModel, OrderReadModelEvent> Generated_CreateView()
        => GeneratedOrderMaterializedView.CreateView();

    [Benchmark(Description = "Fluent: project order read model")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderMaterializedViewSummary> Fluent_ProjectOrderReadModel()
        => OrderMaterializedViewDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: project order read model")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderMaterializedViewSummary> Generated_ProjectOrderReadModel()
        => OrderMaterializedViewDemo.RunGeneratedAsync();
}
