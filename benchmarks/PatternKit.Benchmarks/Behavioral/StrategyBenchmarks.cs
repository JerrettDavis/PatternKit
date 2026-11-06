using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Strategy;
using PatternKit.Generators;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "Strategy")]
public class StrategyBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create strategy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Strategy<CustomerScore, string> Fluent_CreateStrategy()
        => Strategy<CustomerScore, string>.Create()
            .When(static (in CustomerScore score) => score.Value >= 750).Then(static (in CustomerScore _) => "prime")
            .When(static (in CustomerScore score) => score.Value >= 650).Then(static (in CustomerScore _) => "standard")
            .Default(static (in CustomerScore _) => "review")
            .Build();

    [Benchmark(Description = "Generated: create strategy")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedScoreStrategy Generated_CreateStrategy()
        => new GeneratedScoreStrategy.Builder()
            .When(static (in CustomerScore score) => score.Value >= 750).Then(static (in CustomerScore _) => "prime")
            .When(static (in CustomerScore score) => score.Value >= 650).Then(static (in CustomerScore _) => "standard")
            .Default(static (in CustomerScore _) => "review")
            .Build();

    [Benchmark(Description = "Fluent: execute strategy")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_ExecuteStrategy()
        => Fluent_CreateStrategy().Execute(new CustomerScore(720));

    [Benchmark(Description = "Generated: execute strategy")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_ExecuteStrategy()
        => Generated_CreateStrategy().Execute(new CustomerScore(720));
}

public readonly record struct CustomerScore(int Value);

[GenerateStrategy(nameof(GeneratedScoreStrategy), typeof(CustomerScore), typeof(string), StrategyKind.Result)]
public partial class GeneratedScoreStrategyHost;
