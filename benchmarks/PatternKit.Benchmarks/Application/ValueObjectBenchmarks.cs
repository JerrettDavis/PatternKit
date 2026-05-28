using BenchmarkDotNet.Attributes;
using PatternKit.Application.ValueObjects;
using PatternKit.Examples.ValueObjectDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "ValueObject")]
public class ValueObjectBenchmarks
{
    private static readonly OrderValueObjectDemo.Money FluentMoney = OrderValueObjectDemo.Money.Create(25m, "USD").Value;
    private static readonly GeneratedOrderNumber GeneratedNumber = GeneratedOrderNumber.Create("ORD-100", "ONLINE");

    [Benchmark(Baseline = true, Description = "Fluent: create validated value object")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ValueObjectResult<OrderValueObjectDemo.Money> Fluent_CreateMoney()
        => OrderValueObjectDemo.Money.Create(25m, "USD");

    [Benchmark(Description = "Generated: create value object")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedOrderNumber Generated_CreateOrderNumber()
        => GeneratedOrderNumber.Create("ORD-100", "ONLINE");

    [Benchmark(Description = "Fluent: compare value object")]
    [BenchmarkCategory("Fluent", "Execution")]
    public bool Fluent_CompareMoney()
        => FluentMoney.Equals(OrderValueObjectDemo.Money.Create(25m, "USD").Value);

    [Benchmark(Description = "Generated: compare value object")]
    [BenchmarkCategory("Generated", "Execution")]
    public bool Generated_CompareOrderNumber()
        => GeneratedNumber.Equals(GeneratedOrderNumber.Create("ORD-100", "ONLINE"));
}
