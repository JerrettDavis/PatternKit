using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "GuaranteedDelivery")]
public class GuaranteedDeliveryBenchmarks
{
    private static readonly ShipmentDispatchCommand Command = new("ship-100", "UPS", "Chicago");

    [Benchmark(Baseline = true, Description = "Fluent: create guaranteed delivery queue")]
    [BenchmarkCategory("Fluent", "Construction")]
    public GuaranteedDeliveryQueue<ShipmentDispatchCommand> Fluent_CreateQueue()
        => ShipmentGuaranteedDeliveryQueues.Create();

    [Benchmark(Description = "Generated: create guaranteed delivery queue")]
    [BenchmarkCategory("Generated", "Construction")]
    public GuaranteedDeliveryQueue<ShipmentDispatchCommand> Generated_CreateQueue()
        => GeneratedShipmentGuaranteedDeliveryQueue.Create();

    [Benchmark(Description = "Fluent: enqueue receive and acknowledge shipment")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<ShipmentDeliverySummary> Fluent_ProcessShipment()
        => await ShipmentGuaranteedDeliveryExampleRunner.RunFluentAsync(Command).ConfigureAwait(false);

    [Benchmark(Description = "Generated: enqueue receive and acknowledge shipment")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<ShipmentDeliverySummary> Generated_ProcessShipment()
    {
        var service = new ShipmentGuaranteedDeliveryService(GeneratedShipmentGuaranteedDeliveryQueue.Create());
        await service.ScheduleAsync(Command).ConfigureAwait(false);
        return await service.DispatchNextAsync().ConfigureAwait(false);
    }
}
