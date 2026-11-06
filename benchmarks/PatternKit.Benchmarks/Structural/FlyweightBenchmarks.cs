using BenchmarkDotNet.Attributes;
using PatternKit.Generators.Flyweight;
using PatternKit.Structural.Flyweight;

namespace PatternKit.Benchmarks.Structural;

[BenchmarkCategory("Structural", "GoF", "Flyweight")]
public class FlyweightBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create flyweight")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Flyweight<string, SkuStyle> Fluent_CreateFlyweight()
        => Flyweight<string, SkuStyle>
            .Create()
            .WithComparer(StringComparer.OrdinalIgnoreCase)
            .WithFactory(static key => new SkuStyle(key.ToUpperInvariant(), key.Length))
            .Build();

    [Benchmark(Description = "Generated: create flyweight")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedSkuStyleCache Generated_CreateFlyweight()
        => new();

    [Benchmark(Description = "Fluent: resolve style")]
    [BenchmarkCategory("Fluent", "Execution")]
    public int Fluent_ResolveStyle()
    {
        var cache = Fluent_CreateFlyweight();
        return cache.Get("header").Weight + cache.Get("HEADER").Weight;
    }

    [Benchmark(Description = "Generated: resolve style")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_ResolveStyle()
    {
        var cache = new GeneratedSkuStyleCache();
        return cache.Get("header").Weight + cache.Get("HEADER").Weight;
    }
}

public sealed record SkuStyle(string Name, int Weight);

[Flyweight(typeof(string), CacheTypeName = "GeneratedSkuStyleCache", Capacity = 32, Eviction = FlyweightEviction.Lru)]
public readonly partial record struct GeneratedSkuStyle(string Name, int Weight)
{
    [FlyweightFactory]
    private static GeneratedSkuStyle Create(string key)
        => new(key.ToUpperInvariant(), key.Length);
}
