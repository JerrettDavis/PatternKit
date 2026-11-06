using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageBus")]
public class MessageBusBenchmarks
{
    private static readonly BusOrderEvent[] Events =
    [
        new("O-100", "accepted", 125m),
        new("O-101", "paid", 250m)
    ];

    [Benchmark(Baseline = true, Description = "Fluent: create message bus")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessageBus<BusOrderEvent> Fluent_CreateMessageBus()
    {
        var fulfillment = MessageChannel<BusOrderEvent>.Create("fulfillment-orders").Build();
        var billing = MessageChannel<BusOrderEvent>.Create("billing-orders").Build();
        var audit = MessageChannel<BusOrderEvent>.Create("order-audit").Build();
        return OrderMessageBuses.Create(fulfillment, billing, audit);
    }

    [Benchmark(Description = "Generated: create message bus")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessageBus<BusOrderEvent> Generated_CreateMessageBus()
        => GeneratedOrderMessageBus.Create();

    [Benchmark(Description = "Fluent: publish order events")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderMessageBusSummary Fluent_PublishOrderEvents()
        => OrderMessageBusExampleRunner.RunFluent(Events);

    [Benchmark(Description = "Generated: publish order events")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderMessageBusSummary Generated_PublishOrderEvents()
        => OrderMessageBusExampleRunner.RunGeneratedStatic(Events);
}
