using BenchmarkDotNet.Attributes;
using PatternKit.Application.AntiCorruption;
using PatternKit.Examples.AntiCorruptionDemo;

namespace PatternKit.Benchmarks.Application;

[BenchmarkCategory("ApplicationArchitecture", "AntiCorruptionLayer")]
public class AntiCorruptionLayerBenchmarks
{
    private static readonly LegacyOrderDto LegacyOrder = new(" order-100 ", 125m, "USD", " cust-42 ");

    [Benchmark(Baseline = true, Description = "Fluent: create anti-corruption layer")]
    [BenchmarkCategory("Fluent", "Construction")]
    public AntiCorruptionLayer<LegacyOrderDto, CommerceOrder> Fluent_CreateLayer()
        => LegacyOrderAntiCorruptionPolicies.CreateFluentLayer();

    [Benchmark(Description = "Generated: create anti-corruption layer")]
    [BenchmarkCategory("Generated", "Construction")]
    public AntiCorruptionLayer<LegacyOrderDto, CommerceOrder> Generated_CreateLayer()
        => GeneratedLegacyOrderAntiCorruptionLayer.CreateGeneratedLayer();

    [Benchmark(Description = "Fluent: import legacy order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderImportResult Fluent_ImportLegacyOrder()
    {
        var service = new LegacyOrderImportService(new ScriptedLegacyOrderFeed(), LegacyOrderAntiCorruptionPolicies.CreateFluentLayer());
        return service.Import(LegacyOrder);
    }

    [Benchmark(Description = "Generated: import legacy order")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderImportResult Generated_ImportLegacyOrder()
    {
        var service = new LegacyOrderImportService(new ScriptedLegacyOrderFeed(), GeneratedLegacyOrderAntiCorruptionLayer.CreateGeneratedLayer());
        return service.Import(LegacyOrder);
    }
}
