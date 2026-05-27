using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Bridges;
using PatternKit.Messaging.Channels;

namespace PatternKit.Examples.Messaging;

public sealed record PartnerBridgeOrder(string PartnerOrderId, string State, decimal Amount);

public sealed record CommerceBridgeOrderEvent(string OrderId, string Status, decimal Total);

public sealed record PartnerOrderMessagingBridgeSummary(int BridgedCount, int CommerceEventCount, IReadOnlyList<string> Topics, string? CorrelationId);

public sealed record PartnerOrderMessagingBridgeChannels(
    MessageChannel<PartnerBridgeOrder> PartnerOrders,
    MessageChannel<CommerceBridgeOrderEvent> CommerceEvents);

public sealed class PartnerOrderMessagingBridgeService(
    MessagingBridge<PartnerBridgeOrder, CommerceBridgeOrderEvent> bridge,
    PartnerOrderMessagingBridgeChannels channels)
{
    public PartnerOrderMessagingBridgeSummary Import(IEnumerable<PartnerBridgeOrder> orders)
    {
        foreach (var order in orders)
        {
            channels.PartnerOrders.Send(Message<PartnerBridgeOrder>.Create(order)
                .WithCorrelationId($"partner:{order.PartnerOrderId}")
                .WithHeader("source-system", "partner"));
        }

        var results = bridge.BridgeAll();
        var first = channels.CommerceEvents.TryReceive();
        var correlationId = first.Message?.Headers.CorrelationId;
        if (first.Received && first.Message is not null)
            channels.CommerceEvents.Send(first.Message);

        return new(
            results.Count,
            channels.CommerceEvents.Count,
            results.Select(static result => result.Topic!).ToArray(),
            correlationId);
    }
}

public static class PartnerOrderMessagingBridges
{
    public static MessagingBridge<PartnerBridgeOrder, CommerceBridgeOrderEvent> Create(
        MessageChannel<PartnerBridgeOrder> partnerOrders,
        MessageBus<CommerceBridgeOrderEvent> commerceBus)
        => MessagingBridge<PartnerBridgeOrder, CommerceBridgeOrderEvent>.Create("partner-order-bridge")
            .From(partnerOrders)
            .To(commerceBus)
            .PreserveHeaders(static order => new CommerceBridgeOrderEvent(order.PartnerOrderId, order.State, order.Amount))
            .SelectTopic(static message => message.Payload.State == "paid" ? "paid" : "accepted")
            .Build();
}

[GenerateMessagingBridge(typeof(PartnerBridgeOrder), typeof(CommerceBridgeOrderEvent), FactoryName = "Create", BridgeName = "partner-order-bridge")]
public static partial class GeneratedPartnerOrderMessagingBridge;

public sealed class PartnerOrderMessagingBridgeExampleRunner(PartnerOrderMessagingBridgeService service)
{
    public PartnerOrderMessagingBridgeSummary RunGenerated(IEnumerable<PartnerBridgeOrder> orders)
        => service.Import(orders);

    public static PartnerOrderMessagingBridgeSummary RunFluent(IEnumerable<PartnerBridgeOrder> orders)
    {
        var partnerOrders = MessageChannel<PartnerBridgeOrder>.Create("partner-orders").Build();
        var commerceEvents = MessageChannel<CommerceBridgeOrderEvent>.Create("commerce-events").Build();
        var commerceBus = MessageBus<CommerceBridgeOrderEvent>.Create("commerce-bus")
            .Route("accepted", commerceEvents)
            .Route("paid", commerceEvents)
            .Build();
        var bridge = PartnerOrderMessagingBridges.Create(partnerOrders, commerceBus);
        return new PartnerOrderMessagingBridgeService(bridge, new(partnerOrders, commerceEvents)).Import(orders);
    }

    public static PartnerOrderMessagingBridgeSummary RunGeneratedStatic(IEnumerable<PartnerBridgeOrder> orders)
    {
        var partnerOrders = MessageChannel<PartnerBridgeOrder>.Create("partner-orders").Build();
        var commerceEvents = MessageChannel<CommerceBridgeOrderEvent>.Create("commerce-events").Build();
        var commerceBus = MessageBus<CommerceBridgeOrderEvent>.Create("commerce-bus")
            .Route("accepted", commerceEvents)
            .Route("paid", commerceEvents)
            .Build();
        var bridge = GeneratedPartnerOrderMessagingBridge.Create()
            .From(partnerOrders)
            .To(commerceBus)
            .PreserveHeaders(static order => new CommerceBridgeOrderEvent(order.PartnerOrderId, order.State, order.Amount))
            .SelectTopic(static message => message.Payload.State == "paid" ? "paid" : "accepted")
            .Build();
        return new PartnerOrderMessagingBridgeService(bridge, new(partnerOrders, commerceEvents)).Import(orders);
    }
}

public static class PartnerOrderMessagingBridgeServiceCollectionExtensions
{
    public static IServiceCollection AddPartnerOrderMessagingBridgeDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => new PartnerOrderMessagingBridgeChannels(
            MessageChannel<PartnerBridgeOrder>.Create("partner-orders").Build(),
            MessageChannel<CommerceBridgeOrderEvent>.Create("commerce-events").Build()));
        services.AddSingleton(static sp => MessageBus<CommerceBridgeOrderEvent>.Create("commerce-bus")
            .Route("accepted", sp.GetRequiredService<PartnerOrderMessagingBridgeChannels>().CommerceEvents)
            .Route("paid", sp.GetRequiredService<PartnerOrderMessagingBridgeChannels>().CommerceEvents)
            .Build());
        services.AddSingleton(static sp => GeneratedPartnerOrderMessagingBridge.Create()
            .From(sp.GetRequiredService<PartnerOrderMessagingBridgeChannels>().PartnerOrders)
            .To(sp.GetRequiredService<MessageBus<CommerceBridgeOrderEvent>>())
            .PreserveHeaders(static order => new CommerceBridgeOrderEvent(order.PartnerOrderId, order.State, order.Amount))
            .SelectTopic(static message => message.Payload.State == "paid" ? "paid" : "accepted")
            .Build());
        services.AddSingleton<PartnerOrderMessagingBridgeService>();
        services.AddSingleton<PartnerOrderMessagingBridgeExampleRunner>();
        return services;
    }
}
