using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Consumers;

namespace PatternKit.Examples.Messaging;

public sealed record ReplenishmentRequest(string Sku, int Quantity);

public sealed record WarehousePollingSummary(bool Received, string? Sku);

public sealed class WarehousePollingConsumerService(PollingConsumer<ReplenishmentRequest> consumer)
{
    public WarehousePollingSummary Poll()
    {
        var result = consumer.Poll();
        return new(result.Received, result.Message?.Payload.Sku);
    }
}

public static class WarehousePollingConsumers
{
    public static PollingConsumer<ReplenishmentRequest> Create(MessageChannel<ReplenishmentRequest> channel)
        => PollingConsumer<ReplenishmentRequest>.Create("warehouse-replenishment-poller")
            .From(_ => channel.TryReceive().Message)
            .Build();
}

[GeneratePollingConsumer(typeof(ReplenishmentRequest), FactoryName = "Create", ConsumerName = "warehouse-replenishment-poller")]
public static partial class GeneratedWarehousePollingConsumer
{
    private static readonly Queue<Message<ReplenishmentRequest>> Messages = new();

    public static void Enqueue(ReplenishmentRequest request) => Messages.Enqueue(Message<ReplenishmentRequest>.Create(request));

    [PollingConsumerSource]
    private static Message<ReplenishmentRequest>? Poll(MessageContext context) => Messages.Count == 0 ? null : Messages.Dequeue();
}

public sealed class WarehousePollingConsumerExampleRunner(WarehousePollingConsumerService service)
{
    public WarehousePollingSummary RunGenerated() => service.Poll();

    public static WarehousePollingSummary RunFluent(ReplenishmentRequest request)
    {
        var channel = MessageChannel<ReplenishmentRequest>.Create("warehouse-replenishment").Build();
        channel.Send(Message<ReplenishmentRequest>.Create(request));
        return new WarehousePollingConsumerService(WarehousePollingConsumers.Create(channel)).Poll();
    }
}

public static class WarehousePollingConsumerExampleServiceCollectionExtensions
{
    public static IServiceCollection AddWarehousePollingConsumerDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedWarehousePollingConsumer.Create());
        services.AddSingleton<WarehousePollingConsumerService>();
        services.AddSingleton<WarehousePollingConsumerExampleRunner>();
        return services;
    }
}
