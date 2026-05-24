using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "Resequencer")]
public class ResequencerBenchmarks
{
    private static readonly ShipmentEvent[] Events =
    [
        new(2, "ship-100", "packed"),
        new(1, "ship-100", "received"),
        new(3, "ship-100", "shipped")
    ];

    [Benchmark(Baseline = true, Description = "Fluent: create resequencer")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Resequencer<ShipmentEvent> Fluent_CreateResequencer()
        => ShipmentResequencers.Create();

    [Benchmark(Description = "Generated: create resequencer")]
    [BenchmarkCategory("Generated", "Construction")]
    public Resequencer<ShipmentEvent> Generated_CreateResequencer()
        => GeneratedShipmentResequencer.Create();

    [Benchmark(Description = "Fluent: resequence shipment events")]
    [BenchmarkCategory("Fluent", "Execution")]
    public IReadOnlyList<ShipmentResequencerSummary> Fluent_ResequenceShipmentEvents()
        => ShipmentResequencerExampleRunner.RunFluent(Events);

    [Benchmark(Description = "Generated: resequence shipment events")]
    [BenchmarkCategory("Generated", "Execution")]
    public IReadOnlyList<ShipmentResequencerSummary> Generated_ResequenceShipmentEvents()
    {
        var service = new ShipmentResequencerService(GeneratedShipmentResequencer.Create());
        return Events.Select(service.Record).ToArray();
    }
}
