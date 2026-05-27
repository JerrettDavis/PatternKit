using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Bridges;
using PatternKit.Messaging.Channels;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessagingBridge")]
public class MessagingBridgeBenchmarks
{
    private static readonly PartnerBridgeOrder[] Orders =
    [
        new("P-100", "accepted", 125m),
        new("P-101", "paid", 250m)
    ];

    [Benchmark(Baseline = true, Description = "Fluent: create messaging bridge")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessagingBridge<PartnerBridgeOrder, CommerceBridgeOrderEvent> Fluent_CreateMessagingBridge()
    {
        var partnerOrders = MessageChannel<PartnerBridgeOrder>.Create("partner-orders").Build();
        var commerceEvents = MessageChannel<CommerceBridgeOrderEvent>.Create("commerce-events").Build();
        var commerceBus = MessageBus<CommerceBridgeOrderEvent>.Create("commerce-bus")
            .Route("accepted", commerceEvents)
            .Route("paid", commerceEvents)
            .Build();
        return PartnerOrderMessagingBridges.Create(partnerOrders, commerceBus);
    }

    [Benchmark(Description = "Generated: create messaging bridge")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessagingBridge<PartnerBridgeOrder, CommerceBridgeOrderEvent> Generated_CreateMessagingBridge()
    {
        var partnerOrders = MessageChannel<PartnerBridgeOrder>.Create("partner-orders").Build();
        var commerceEvents = MessageChannel<CommerceBridgeOrderEvent>.Create("commerce-events").Build();
        var commerceBus = MessageBus<CommerceBridgeOrderEvent>.Create("commerce-bus")
            .Route("accepted", commerceEvents)
            .Route("paid", commerceEvents)
            .Build();
        return GeneratedPartnerOrderMessagingBridge.Create()
            .From(partnerOrders)
            .To(commerceBus)
            .PreserveHeaders(static order => new CommerceBridgeOrderEvent(order.PartnerOrderId, order.State, order.Amount))
            .SelectTopic(static message => message.Payload.State == "paid" ? "paid" : "accepted")
            .Build();
    }

    [Benchmark(Description = "Fluent: bridge partner orders")]
    [BenchmarkCategory("Fluent", "Execution")]
    public PartnerOrderMessagingBridgeSummary Fluent_BridgePartnerOrders()
        => PartnerOrderMessagingBridgeExampleRunner.RunFluent(Orders);

    [Benchmark(Description = "Generated: bridge partner orders")]
    [BenchmarkCategory("Generated", "Execution")]
    public PartnerOrderMessagingBridgeSummary Generated_BridgePartnerOrders()
        => PartnerOrderMessagingBridgeExampleRunner.RunGeneratedStatic(Orders);
}
