using BenchmarkDotNet.Attributes;
using PatternKit.Application.Repository;
using PatternKit.Examples.RepositoryDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "Repository")]
public class RepositoryBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create repository")]
    [BenchmarkCategory("Fluent", "Construction")]
    public InMemoryRepository<OrderRecord, string> Fluent_CreateRepository()
        => OrderRepositoryPolicies.CreateFluentRepository();

    [Benchmark(Description = "Generated: create repository")]
    [BenchmarkCategory("Generated", "Construction")]
    public InMemoryRepository<OrderRecord, string> Generated_CreateRepository()
        => GeneratedOrderRepository.CreateRepository();

    [Benchmark(Description = "Fluent: seed and query repository")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<OrderRepositorySummary> Fluent_SeedAndQuery()
        => OrderRepositoryDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: seed and query repository")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<OrderRepositorySummary> Generated_SeedAndQuery()
        => OrderRepositoryDemo.RunGeneratedAsync();
}
