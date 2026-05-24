using BenchmarkDotNet.Attributes;
using PatternKit.EnterpriseIntegration.EventCarriedStateTransfer;
using PatternKit.Examples.EventCarriedStateTransferDemo;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "EventCarriedStateTransfer")]
public class EventCarriedStateTransferBenchmarks
{
    private static readonly InventoryAdjustedEvent Event = new("SKU-100", 12, "CHI-01", 4);

    [Benchmark(Baseline = true, Description = "Fluent: create event-carried state transfer")]
    [BenchmarkCategory("Fluent", "Construction")]
    public EventCarriedStateTransfer<InventoryAdjustedEvent, string, InventoryReadModel> Fluent_CreateStateTransfer()
        => InventoryStateTransfers.CreateFluent();

    [Benchmark(Description = "Generated: create event-carried state transfer")]
    [BenchmarkCategory("Generated", "Construction")]
    public EventCarriedStateTransfer<InventoryAdjustedEvent, string, InventoryReadModel> Generated_CreateStateTransfer()
        => GeneratedInventoryStateTransfer.Create();

    [Benchmark(Description = "Fluent: project carried inventory state")]
    [BenchmarkCategory("Fluent", "Execution")]
    public InventoryProjectionSummary Fluent_ProjectCarriedInventoryState()
        => new InventoryProjectionService(InventoryStateTransfers.CreateFluent(), new InMemoryInventoryReadModelStore()).Project(Event);

    [Benchmark(Description = "Generated: project carried inventory state")]
    [BenchmarkCategory("Generated", "Execution")]
    public InventoryProjectionSummary Generated_ProjectCarriedInventoryState()
        => new InventoryProjectionService(GeneratedInventoryStateTransfer.Create(), new InMemoryInventoryReadModelStore()).Project(Event);
}
