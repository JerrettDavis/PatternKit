using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Consumers;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "DurableSubscriber")]
public class DurableSubscriberBenchmarks
{
    private static readonly OrderShipmentEvent[] Events = [new("O-100", "Packed", "central"), new("O-101", "Shipped", "central")];

    [Benchmark(Baseline = true, Description = "Fluent: create durable subscriber")]
    [BenchmarkCategory("Fluent", "Construction")]
    public DurableSubscriber<OrderShipmentEvent> Fluent_CreateDurableSubscriber()
    {
        var store = OrderDurableSubscribers.CreateStore();
        return OrderDurableSubscribers.Create(store, new InMemoryDurableSubscriberCheckpointStore(), new OrderShipmentProjection());
    }

    [Benchmark(Description = "Generated: create durable subscriber")]
    [BenchmarkCategory("Generated", "Construction")]
    public DurableSubscriber<OrderShipmentEvent> Generated_CreateDurableSubscriber()
        => GeneratedOrderDurableSubscriber.Create(OrderDurableSubscribers.CreateStore(), new InMemoryDurableSubscriberCheckpointStore());

    [Benchmark(Description = "Fluent: catch up shipment projection")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderDurableSubscriberSummary Fluent_CatchUpShipmentProjection()
        => OrderDurableSubscriberExampleRunner.RunFluent(Events);

    [Benchmark(Description = "Generated: catch up shipment projection")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderDurableSubscriberSummary Generated_CatchUpShipmentProjection()
        => OrderDurableSubscriberExampleRunner.RunGeneratedStatic(Events);
}
