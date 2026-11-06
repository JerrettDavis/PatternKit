using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "WireTap")]
public class WireTapBenchmarks
{
    private static readonly OrderWireTapEvent Event = new("order-100", "tenant-a", 149.95m);

    [Benchmark(Baseline = true, Description = "Fluent: create wire tap")]
    [BenchmarkCategory("Fluent", "Construction")]
    public WireTap<OrderWireTapEvent> Fluent_CreateWireTap()
        => OrderWireTaps.Create(new OrderWireTapAuditSink(), new OrderWireTapMetricsSink());

    [Benchmark(Description = "Generated: create wire tap")]
    [BenchmarkCategory("Generated", "Construction")]
    public WireTap<OrderWireTapEvent> Generated_CreateWireTap()
    {
        OrderWireTapSinkRegistry.Audit = new OrderWireTapAuditSink();
        OrderWireTapSinkRegistry.Metrics = new OrderWireTapMetricsSink();
        return GeneratedOrderWireTap.Create();
    }

    [Benchmark(Description = "Fluent: publish order event")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderWireTapSummary Fluent_PublishOrderEvent()
        => OrderWireTapExampleRunner.RunFluent(Event);

    [Benchmark(Description = "Generated: publish order event")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderWireTapSummary Generated_PublishOrderEvent()
    {
        var audit = new OrderWireTapAuditSink();
        var metrics = new OrderWireTapMetricsSink();
        OrderWireTapSinkRegistry.Audit = audit;
        OrderWireTapSinkRegistry.Metrics = metrics;

        var service = new OrderWireTapService(GeneratedOrderWireTap.Create(), audit, metrics);
        return service.Publish(Event);
    }
}
