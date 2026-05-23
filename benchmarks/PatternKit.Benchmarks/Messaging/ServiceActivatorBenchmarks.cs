using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Activation;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ServiceActivator")]
public class ServiceActivatorBenchmarks
{
    private static readonly InventoryReservationRequest Request = new("SKU-42", 12);
    private readonly InventoryServiceActivatorService _fluentService = new(InventoryServiceActivators.Create());
    private readonly InventoryServiceActivatorService _generatedService = new(GeneratedInventoryServiceActivator.Create());

    [Benchmark(Baseline = true, Description = "Fluent: create service activator")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ServiceActivator<InventoryReservationRequest, InventoryReservationResult> Fluent_CreateActivator()
        => InventoryServiceActivators.Create();

    [Benchmark(Description = "Generated: create service activator")]
    [BenchmarkCategory("Generated", "Construction")]
    public ServiceActivator<InventoryReservationRequest, InventoryReservationResult> Generated_CreateActivator()
        => GeneratedInventoryServiceActivator.Create();

    [Benchmark(Description = "Fluent: reserve inventory")]
    [BenchmarkCategory("Fluent", "Execution")]
    public InventoryServiceActivatorSummary Fluent_ReserveInventory()
        => _fluentService.Reserve(Request);

    [Benchmark(Description = "Generated: reserve inventory")]
    [BenchmarkCategory("Generated", "Execution")]
    public InventoryServiceActivatorSummary Generated_ReserveInventory()
        => _generatedService.Reserve(Request);
}
