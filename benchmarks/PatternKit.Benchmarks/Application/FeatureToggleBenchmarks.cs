using BenchmarkDotNet.Attributes;
using PatternKit.Application.FeatureToggles;
using PatternKit.Examples.FeatureToggleDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "FeatureToggle")]
public class FeatureToggleBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create feature toggle set")]
    [BenchmarkCategory("Fluent", "Construction")]
    public IFeatureToggleSet<CheckoutFeatureContext> Fluent_CreateToggleSet()
        => CheckoutFeatureTogglePolicies.CreateFluentToggleSet();

    [Benchmark(Description = "Generated: create feature toggle set")]
    [BenchmarkCategory("Generated", "Construction")]
    public IFeatureToggleSet<CheckoutFeatureContext> Generated_CreateToggleSet()
        => GeneratedCheckoutFeatureToggles.CreateToggles();

    [Benchmark(Description = "Fluent: evaluate checkout features")]
    [BenchmarkCategory("Fluent", "Execution")]
    public CheckoutFeatureToggleSummary Fluent_EvaluateCheckoutFeatures()
        => CheckoutFeatureToggleDemo.RunFluent();

    [Benchmark(Description = "Generated: evaluate checkout features")]
    [BenchmarkCategory("Generated", "Execution")]
    public CheckoutFeatureToggleSummary Generated_EvaluateCheckoutFeatures()
        => CheckoutFeatureToggleDemo.RunGenerated();
}
