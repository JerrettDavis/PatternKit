using BenchmarkDotNet.Attributes;
using PatternKit.Application.CompensatingTransactions;
using PatternKit.Examples.CompensatingTransactionDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "CompensatingTransaction")]
public class CompensatingTransactionBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create compensating transaction")]
    [BenchmarkCategory("Fluent", "Construction")]
    public CompensatingTransaction<CheckoutCompensatingTransactionContext> Fluent_CreateTransaction()
        => CheckoutCompensatingTransactionDemo.CreateFluent();

    [Benchmark(Description = "Generated: create compensating transaction")]
    [BenchmarkCategory("Generated", "Construction")]
    public CompensatingTransaction<CheckoutCompensatingTransactionContext> Generated_CreateTransaction()
        => GeneratedCheckoutCompensatingTransaction.Create();

    [Benchmark(Description = "Fluent: execute compensated checkout")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<CheckoutCompensatingTransactionSummary> Fluent_ExecuteCompensatedCheckout()
        => CheckoutCompensatingTransactionDemo.RunFluentAsync();

    [Benchmark(Description = "Generated: execute compensated checkout")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<CheckoutCompensatingTransactionSummary> Generated_ExecuteCompensatedCheckout()
        => CheckoutCompensatingTransactionDemo.RunGeneratedAsync();
}
