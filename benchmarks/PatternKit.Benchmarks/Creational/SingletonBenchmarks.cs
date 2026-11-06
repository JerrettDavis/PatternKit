using BenchmarkDotNet.Attributes;
using PatternKit.Creational.Singleton;
using PatternKit.Examples.Singleton;
using PatternKit.Examples.SingletonGeneratorDemo;

namespace PatternKit.Benchmarks.Creational;

[BenchmarkCategory("Creational", "GoF", "Singleton")]
public class SingletonBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create singleton")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Singleton<PosAppState> Fluent_CreateSingleton()
        => PosAppStateDemo.BuildLazy();

    [Benchmark(Description = "Generated: resolve singleton surface")]
    [BenchmarkCategory("Generated", "Construction")]
    public Type Generated_ResolveSingletonSurface()
        => typeof(ConfigManager);

    [Benchmark(Description = "Fluent: access singleton instance")]
    [BenchmarkCategory("Fluent", "Execution")]
    public int Fluent_AccessSingletonInstance()
    {
        PosAppStateDemo.ResetCounters();
        var singleton = PosAppStateDemo.BuildLazy();
        return singleton.Instance.Pricing.WarmedCount + singleton.Instance.Log.Count;
    }

    [Benchmark(Description = "Generated: access singleton instance")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_AccessSingletonInstance()
        => ConfigManager.Instance.ConnectionString;
}
