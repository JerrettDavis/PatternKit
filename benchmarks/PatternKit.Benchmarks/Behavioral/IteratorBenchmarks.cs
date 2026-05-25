using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Iterator;
using PatternKit.Generators.Iterator;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "Iterator")]
public class IteratorBenchmarks
{
    private static readonly RevenueLine[] Lines =
    [
        new(120m, true),
        new(80m, false),
        new(45m, true),
        new(20m, true)
    ];

    [Benchmark(Baseline = true, Description = "Fluent: create iterator flow")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Flow<decimal> Fluent_CreateIterator()
        => Flow<RevenueLine>.From(Lines)
            .Filter(static line => line.IsBillable)
            .Map(static line => line.Amount);

    [Benchmark(Description = "Generated: create iterator")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedRevenueIterator Generated_CreateIterator()
        => new(Lines);

    [Benchmark(Description = "Fluent: iterate revenue lines")]
    [BenchmarkCategory("Fluent", "Execution")]
    public decimal Fluent_IterateRevenue()
        => Fluent_CreateIterator().Fold(0m, static (total, amount) => total + amount);

    [Benchmark(Description = "Generated: iterate revenue lines")]
    [BenchmarkCategory("Generated", "Execution")]
    public decimal Generated_IterateRevenue()
    {
        var iterator = new GeneratedRevenueIterator(Lines);
        var total = 0m;
        while (iterator.TryMoveNext(out var amount))
            total += amount;
        return total;
    }
}

public readonly record struct RevenueLine(decimal Amount, bool IsBillable);

[Iterator]
public partial class GeneratedRevenueIterator
{
    private readonly RevenueLine[] _lines;
    private int _index;

    public GeneratedRevenueIterator(RevenueLine[] lines)
    {
        _lines = lines;
        _index = 0;
    }

    [IteratorStep]
    private bool Step(out decimal amount)
    {
        while (_index < _lines.Length)
        {
            var current = _lines[_index++];
            if (!current.IsBillable)
                continue;

            amount = current.Amount;
            return true;
        }

        amount = 0m;
        return false;
    }
}
