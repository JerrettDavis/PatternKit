using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Examples.Messaging;

public sealed record OrderImportCommand(string OrderId, string Sku, int Quantity);

public sealed record OrderInvalidMessageSummary(
    int AcceptedCount,
    int InvalidCount,
    IReadOnlyList<string> InvalidReasons,
    IReadOnlyList<string> InvalidOrderIds);

public sealed class OrderInvalidMessageChannelService(InvalidMessageChannel<OrderImportCommand> invalids)
{
    public OrderInvalidMessageSummary Import(IEnumerable<OrderImportCommand> commands)
    {
        var accepted = 0;
        foreach (var command in commands)
        {
            var message = Message<OrderImportCommand>.Create(command)
                .WithMessageId(command.OrderId)
                .WithCorrelationId(command.OrderId);

            var result = invalids.Route(message);
            if (!result.Routed)
                accepted++;
        }

        var invalidMessages = invalids.Snapshot();
        return new(
            accepted,
            invalidMessages.Count,
            invalidMessages.Select(message => message.Payload.Reason).ToArray(),
            invalidMessages.Select(message => message.Payload.OriginalMessage.Payload.OrderId).ToArray());
    }
}

public static class OrderInvalidMessageChannels
{
    public static InvalidMessageChannel<OrderImportCommand> Create(MessageChannel<InvalidMessage<OrderImportCommand>> invalids)
        => InvalidMessageChannel<OrderImportCommand>.Create("order-import-invalids")
            .To(invalids)
            .When(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) || message.Payload.Quantity <= 0)
            .Because(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) ? "SKU is required." : "Quantity must be positive.")
            .Build();
}

[GenerateInvalidMessageChannel(typeof(OrderImportCommand), FactoryName = "Create", ChannelName = "order-import-invalids")]
public static partial class GeneratedOrderInvalidMessageChannel;

public sealed class OrderInvalidMessageChannelExampleRunner(OrderInvalidMessageChannelService service)
{
    public OrderInvalidMessageSummary RunGenerated(IEnumerable<OrderImportCommand> commands)
        => service.Import(commands);

    public static OrderInvalidMessageSummary RunFluent(IEnumerable<OrderImportCommand> commands)
    {
        var invalids = CreateInvalidChannel();
        var service = new OrderInvalidMessageChannelService(OrderInvalidMessageChannels.Create(invalids));
        return service.Import(commands);
    }

    public static MessageChannel<InvalidMessage<OrderImportCommand>> CreateInvalidChannel()
        => MessageChannel<InvalidMessage<OrderImportCommand>>.Create("invalid-order-imports")
            .WithCapacity(128)
            .Build();
}

public static class OrderInvalidMessageChannelServiceCollectionExtensions
{
    public static IServiceCollection AddOrderInvalidMessageChannelDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => OrderInvalidMessageChannelExampleRunner.CreateInvalidChannel());
        services.AddSingleton(sp => GeneratedOrderInvalidMessageChannel.Create(
                sp.GetRequiredService<MessageChannel<InvalidMessage<OrderImportCommand>>>())
            .When(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) || message.Payload.Quantity <= 0)
            .Because(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) ? "SKU is required." : "Quantity must be positive.")
            .Build());
        services.AddSingleton<OrderInvalidMessageChannelService>();
        services.AddSingleton<OrderInvalidMessageChannelExampleRunner>();
        return services;
    }
}
