using BenchmarkDotNet.Attributes;
using PatternKit.Application.ContextMaps;
using PatternKit.Examples.ContextMapDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "ContextMap")]
public class ContextMapBenchmarks
{
    private readonly ContextMapDescriptor _fluent = CommerceContextMapDemo.CreateFluentMap();

    private readonly ContextMapDescriptor _generated = CommerceContextMapDemo.CreateGeneratedMap();

    [Benchmark(Baseline = true, Description = "Fluent: create context map descriptor")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ContextMapDescriptor Fluent_CreateMap()
        => CommerceContextMapDemo.CreateFluentMap();

    [Benchmark(Description = "Generated: create context map descriptor")]
    [BenchmarkCategory("Generated", "Construction")]
    public ContextMapDescriptor Generated_CreateMap()
        => CommerceContextMapDemo.CreateGeneratedMap();

    [Benchmark(Description = "Fluent: inspect context map relationships")]
    [BenchmarkCategory("Fluent", "Execution")]
    public int Fluent_InspectRelationships()
        => _fluent.Relationships.Count(static relationship => relationship.Kind == ContextRelationshipKind.PublishedLanguage);

    [Benchmark(Description = "Generated: inspect context map relationships")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_InspectRelationships()
        => _generated.Relationships.Count(static relationship => relationship.Kind == ContextRelationshipKind.PublishedLanguage);
}
