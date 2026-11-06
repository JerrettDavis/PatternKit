using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Storage;

namespace PatternKit.Examples.Messaging;

/// <summary>Order audit event stored for replay and support lookup.</summary>
public sealed record OrderMessageStoreEvent(string OrderId, string EventName, decimal Total, bool ContainsSensitiveData);

/// <summary>Summary returned by the message-store example.</summary>
public sealed record OrderMessageStoreSummary(bool Stored, bool Duplicate, int ReplayCount, string? RejectionReason);

/// <summary>Service that records order events in a DI-importable message store.</summary>
public sealed class OrderMessageStoreService(MessageStore<OrderMessageStoreEvent> store)
{
    public OrderMessageStoreSummary Record(OrderMessageStoreEvent orderEvent, string messageId, string correlationId)
    {
        var message = Message<OrderMessageStoreEvent>.Create(orderEvent)
            .WithMessageId(messageId)
            .WithCorrelationId(correlationId);
        var result = store.Append(message);
        var replay = store.Replay(MessageStoreQuery.ForCorrelation(correlationId));
        return new OrderMessageStoreSummary(result.Stored, result.Duplicate, replay.Count, result.RejectionReason);
    }
}

/// <summary>Fluent message-store builder used by applications that do not enable generators.</summary>
public static class OrderMessageStores
{
    public static MessageStore<OrderMessageStoreEvent> CreateAuditStore()
        => MessageStore<OrderMessageStoreEvent>.Create("order-message-store")
            .IdentifyBy(static (message, _) => message.Headers.MessageId!)
            .RetainWhen(static stored => !stored.Message.Payload.ContainsSensitiveData)
            .Build();
}

/// <summary>Source-generated message store for order audit and replay events.</summary>
[GenerateMessageStore(typeof(OrderMessageStoreEvent), FactoryName = "Create", StoreName = "order-message-store")]
public static partial class GeneratedOrderMessageStore
{
    [MessageStoreIdentity]
    private static string SelectIdentity(Message<OrderMessageStoreEvent> message, MessageContext context)
        => message.Headers.MessageId!;

    [MessageStoreRetention]
    private static bool RetainReplaySafeEvents(StoredMessage<OrderMessageStoreEvent> stored)
        => !stored.Message.Payload.ContainsSensitiveData;
}

/// <summary>Runner that demonstrates both fluent and generated message-store paths.</summary>
public sealed class OrderMessageStoreExampleRunner(OrderMessageStoreService service)
{
    public OrderMessageStoreSummary RunGenerated(OrderMessageStoreEvent orderEvent, string messageId, string correlationId)
        => service.Record(orderEvent, messageId, correlationId);

    public static OrderMessageStoreSummary RunFluent(OrderMessageStoreEvent orderEvent, string messageId, string correlationId)
    {
        var store = OrderMessageStores.CreateAuditStore();
        var message = Message<OrderMessageStoreEvent>.Create(orderEvent)
            .WithMessageId(messageId)
            .WithCorrelationId(correlationId);
        var result = store.Append(message);
        var replay = store.Replay(MessageStoreQuery.ForCorrelation(correlationId));
        return new OrderMessageStoreSummary(result.Stored, result.Duplicate, replay.Count, result.RejectionReason);
    }
}

/// <summary>DI helpers for importing the order message-store example into standard .NET containers.</summary>
public static class OrderMessageStoreExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderMessageStoreDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedOrderMessageStore.Create());
        services.AddSingleton<OrderMessageStoreService>();
        services.AddSingleton<OrderMessageStoreExampleRunner>();
        return services;
    }
}
