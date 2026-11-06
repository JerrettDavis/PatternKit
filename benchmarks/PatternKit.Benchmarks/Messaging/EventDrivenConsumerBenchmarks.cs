using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Consumers;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "EventDrivenConsumer")]
public class EventDrivenConsumerBenchmarks
{
    private static readonly OrderAcceptedEvent Event = new("O-100", 149.95m);

    [Benchmark(Baseline = true, Description = "Fluent: create event-driven consumer")]
    [BenchmarkCategory("Fluent", "Construction")]
    public EventDrivenConsumer<OrderAcceptedEvent> Fluent_CreateEventDrivenConsumer()
        => OrderEventDrivenConsumers.Create(new OrderEventDrivenAuditSink());

    [Benchmark(Description = "Generated: create event-driven consumer")]
    [BenchmarkCategory("Generated", "Construction")]
    public EventDrivenConsumer<OrderAcceptedEvent> Generated_CreateEventDrivenConsumer()
        => GeneratedOrderEventDrivenConsumer.Create();

    [Benchmark(Description = "Fluent: accept order event")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderEventDrivenSummary Fluent_AcceptOrderEvent()
        => OrderEventDrivenConsumerExampleRunner.RunFluent(Event);

    [Benchmark(Description = "Generated: accept order event")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderEventDrivenSummary Generated_AcceptOrderEvent()
        => OrderEventDrivenConsumerExampleRunner.RunGeneratedStatic(Event);
}
