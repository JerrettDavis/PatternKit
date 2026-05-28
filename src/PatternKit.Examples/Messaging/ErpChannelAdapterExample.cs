using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Adapters;
using PatternKit.Messaging.Channels;

namespace PatternKit.Examples.Messaging;

public sealed record ErpOrderDocument(string ExternalOrderId, string Total);

public sealed record OrderIntegrationMessage(string OrderId, decimal Total);

public sealed record ErpChannelAdapterSummary(bool Imported, string? OrderId, bool Exported, string? ExternalTotal);

public sealed record ErpChannelAdapterChannels(
    MessageChannel<OrderIntegrationMessage> Inbound,
    MessageChannel<OrderIntegrationMessage> Outbound);

public sealed class ErpChannelAdapterService(
    ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage> adapter,
    ErpChannelAdapterChannels channels)
{
    public ErpChannelAdapterSummary RoundTrip(ErpOrderDocument document)
    {
        var inbound = adapter.AcceptExternal(document);
        var imported = channels.Inbound.TryReceive();
        if (imported.Message is not null)
            channels.Outbound.Send(imported.Message);

        var exported = adapter.TryTakeExternal();
        return new(
            inbound.Accepted,
            imported.Message?.Payload.OrderId,
            exported.Produced,
            exported.External?.Total);
    }
}

public static class ErpChannelAdapters
{
    public static ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage> Create(
        MessageChannel<OrderIntegrationMessage> inboundChannel,
        MessageChannel<OrderIntegrationMessage> outboundChannel)
        => ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage>.Create("erp-orders-adapter")
            .ReceiveInto(inboundChannel)
            .SendFrom(outboundChannel)
            .MapInbound(ToMessage)
            .MapOutbound(ToExternal)
            .Build();

    public static Message<OrderIntegrationMessage> ToMessage(ErpOrderDocument document, MessageContext context)
        => Message<OrderIntegrationMessage>.Create(new(
            document.ExternalOrderId,
            decimal.Parse(document.Total, CultureInfo.InvariantCulture)));

    public static ErpOrderDocument ToExternal(Message<OrderIntegrationMessage> message, MessageContext context)
        => new(
            message.Payload.OrderId,
            message.Payload.Total.ToString("0.00", CultureInfo.InvariantCulture));
}

[GenerateChannelAdapter(typeof(ErpOrderDocument), typeof(OrderIntegrationMessage), FactoryName = "Create", AdapterName = "erp-orders-adapter")]
public static partial class GeneratedErpChannelAdapter
{
    [ChannelAdapterInbound]
    private static Message<OrderIntegrationMessage> ToMessage(ErpOrderDocument document, MessageContext context)
        => ErpChannelAdapters.ToMessage(document, context);

    [ChannelAdapterOutbound]
    private static ErpOrderDocument ToExternal(Message<OrderIntegrationMessage> message, MessageContext context)
        => ErpChannelAdapters.ToExternal(message, context);
}

public sealed class ErpChannelAdapterExampleRunner(ErpChannelAdapterService service)
{
    public ErpChannelAdapterSummary RunGenerated(ErpOrderDocument document) => service.RoundTrip(document);

    public static ErpChannelAdapterSummary RunFluent(ErpOrderDocument document)
    {
        var inbound = MessageChannel<OrderIntegrationMessage>.Create("erp-inbound").Build();
        var outbound = MessageChannel<OrderIntegrationMessage>.Create("erp-outbound").Build();
        var adapter = ErpChannelAdapters.Create(inbound, outbound);
        return new ErpChannelAdapterService(adapter, new(inbound, outbound)).RoundTrip(document);
    }

    public static ErpChannelAdapterSummary RunGeneratedStatic(ErpOrderDocument document)
    {
        var inbound = MessageChannel<OrderIntegrationMessage>.Create("erp-inbound").Build();
        var outbound = MessageChannel<OrderIntegrationMessage>.Create("erp-outbound").Build();
        var adapter = GeneratedErpChannelAdapter.Create(inbound, outbound);
        return new ErpChannelAdapterService(adapter, new(inbound, outbound)).RoundTrip(document);
    }
}

public static class ErpChannelAdapterExampleServiceCollectionExtensions
{
    public static IServiceCollection AddErpChannelAdapterDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => new ErpChannelAdapterChannels(
            MessageChannel<OrderIntegrationMessage>.Create("erp-inbound").Build(),
            MessageChannel<OrderIntegrationMessage>.Create("erp-outbound").Build()));
        services.AddSingleton(sp => GeneratedErpChannelAdapter.Create(
            sp.GetRequiredService<ErpChannelAdapterChannels>().Inbound,
            sp.GetRequiredService<ErpChannelAdapterChannels>().Outbound));
        services.AddSingleton<ErpChannelAdapterService>();
        services.AddSingleton<ErpChannelAdapterExampleRunner>();
        return services;
    }
}
