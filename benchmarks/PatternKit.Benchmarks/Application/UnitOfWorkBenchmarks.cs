using BenchmarkDotNet.Attributes;
using PatternKit.Application.UnitOfWork;
using PatternKit.Examples.UnitOfWorkDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "UnitOfWork")]
public class UnitOfWorkBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create unit of work")]
    [BenchmarkCategory("Fluent", "Construction")]
    public UnitOfWork Fluent_CreateUnitOfWork()
        => UnitOfWork.Create()
            .Enlist("reserve-inventory", static _ => default, static _ => default)
            .Enlist("capture-payment", static _ => default, static _ => default)
            .Build();

    [Benchmark(Description = "Generated: create unit of work")]
    [BenchmarkCategory("Generated", "Construction")]
    public UnitOfWork Generated_CreateUnitOfWork()
        => GeneratedCheckoutUnitOfWork.Create();

    [Benchmark(Description = "Fluent: commit checkout unit")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<CheckoutUnitOfWorkSummary> Fluent_CommitCheckoutUnit()
        => CheckoutUnitOfWorkDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: commit checkout unit")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<CheckoutUnitOfWorkSummary> Generated_CommitCheckoutUnit()
        => CheckoutUnitOfWorkDemo.RunGeneratedAsync();
}
