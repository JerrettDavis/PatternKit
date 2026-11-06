using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Examples.Messaging;

public sealed record InventoryAdjustment(string Sku, int Quantity, string Reason);

public sealed record InventoryChannelSummary(bool Accepted, string? ReceivedSku, int RemainingMessages, string? RejectionReason);

public sealed class InventoryMessageChannelService(MessageChannel<InventoryAdjustment> channel)
{
    public InventoryChannelSummary Enqueue(InventoryAdjustment adjustment)
    {
        var result = channel.Send(Message<InventoryAdjustment>.Create(adjustment).WithCorrelationId(adjustment.Sku));
        return new(result.Accepted, null, result.Count, result.RejectionReason);
    }

    public InventoryChannelSummary TryProcessNext()
    {
        var result = channel.TryReceive();
        return new(true, result.Message?.Payload.Sku, result.Count, null);
    }
}

public static class InventoryMessageChannels
{
    public static MessageChannel<InventoryAdjustment> Create()
        => MessageChannel<InventoryAdjustment>.Create("inventory-adjustments")
            .WithCapacity(32)
            .Build();
}

[GenerateMessageChannel(typeof(InventoryAdjustment), FactoryName = "Create", ChannelName = "inventory-adjustments", Capacity = 32)]
public static partial class GeneratedInventoryMessageChannel;

public sealed class InventoryMessageChannelExampleRunner(InventoryMessageChannelService service)
{
    public InventoryChannelSummary RunGenerated(InventoryAdjustment adjustment)
    {
        service.Enqueue(adjustment);
        return service.TryProcessNext();
    }

    public static InventoryChannelSummary RunFluent(InventoryAdjustment adjustment)
    {
        var service = new InventoryMessageChannelService(InventoryMessageChannels.Create());
        service.Enqueue(adjustment);
        return service.TryProcessNext();
    }
}

public static class InventoryMessageChannelExampleServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryMessageChannelDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedInventoryMessageChannel.Create());
        services.AddSingleton<InventoryMessageChannelService>();
        services.AddSingleton<InventoryMessageChannelExampleRunner>();
        return services;
    }
}
