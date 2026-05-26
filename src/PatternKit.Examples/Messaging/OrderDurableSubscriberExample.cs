using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Consumers;
using PatternKit.Messaging.Storage;

namespace PatternKit.Examples.Messaging;

public sealed record OrderShipmentEvent(string OrderId, string Status, string Region);

public sealed record OrderDurableSubscriberSummary(bool Completed, int DeliveredCount, int SkippedCount, long LastSequence, IReadOnlyList<string> ProjectionEntries);

public sealed class OrderShipmentProjection
{
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries.AsReadOnly();

    public void Apply(OrderShipmentEvent shipmentEvent)
        => _entries.Add($"{shipmentEvent.Region}:{shipmentEvent.OrderId}:{shipmentEvent.Status}");

    public void Clear() => _entries.Clear();
}

public sealed class OrderDurableSubscriberService(
    MessageStore<OrderShipmentEvent> store,
    DurableSubscriber<OrderShipmentEvent> subscriber,
    OrderShipmentProjection projection)
{
    public OrderDurableSubscriberSummary CatchUp(IEnumerable<OrderShipmentEvent> events)
    {
        var index = 0;
        foreach (var shipmentEvent in events)
        {
            index++;
            _ = store.Append(Message<OrderShipmentEvent>.Create(shipmentEvent)
                .WithMessageId($"shipment-{index}")
                .WithCorrelationId(shipmentEvent.OrderId));
        }

        var result = subscriber.CatchUp();
        return new(result.Completed, result.DeliveredCount, result.SkippedCount, result.LastSequence, projection.Entries);
    }
}

public static class OrderDurableSubscribers
{
    public static MessageStore<OrderShipmentEvent> CreateStore()
        => MessageStore<OrderShipmentEvent>.Create("order-shipment-events")
            .IdentifyBy(static (message, _) => message.Headers.MessageId!)
            .Build();

    public static DurableSubscriber<OrderShipmentEvent> Create(MessageStore<OrderShipmentEvent> store, IDurableSubscriberCheckpointStore checkpoints, OrderShipmentProjection projection)
        => DurableSubscriber<OrderShipmentEvent>.Create("shipment-projection")
            .From(store)
            .TrackWith(checkpoints)
            .Handle("project", (stored, _) =>
            {
                projection.Apply(stored.Message.Payload);
                return DurableSubscriberHandlerResult.Success("project");
            })
            .Build();
}

[GenerateDurableSubscriber(typeof(OrderShipmentEvent), FactoryName = "Create", SubscriberName = "shipment-projection")]
public static partial class GeneratedOrderDurableSubscriber
{
    private static readonly OrderShipmentProjection Projection = new();

    public static IReadOnlyList<string> Entries => Projection.Entries;

    public static void Reset() => Projection.Clear();

    [DurableSubscriberHandler("project")]
    private static DurableSubscriberHandlerResult Project(StoredMessage<OrderShipmentEvent> message, MessageContext context)
    {
        Projection.Apply(message.Message.Payload);
        return DurableSubscriberHandlerResult.Success("project");
    }
}

public sealed class OrderDurableSubscriberExampleRunner(OrderDurableSubscriberService service)
{
    public OrderDurableSubscriberSummary RunGenerated(IEnumerable<OrderShipmentEvent> events) => service.CatchUp(events);

    public static OrderDurableSubscriberSummary RunFluent(IEnumerable<OrderShipmentEvent> events)
    {
        var store = OrderDurableSubscribers.CreateStore();
        var checkpoints = new InMemoryDurableSubscriberCheckpointStore();
        var projection = new OrderShipmentProjection();
        var subscriber = OrderDurableSubscribers.Create(store, checkpoints, projection);
        return new OrderDurableSubscriberService(store, subscriber, projection).CatchUp(events);
    }

    public static OrderDurableSubscriberSummary RunGeneratedStatic(IEnumerable<OrderShipmentEvent> events)
    {
        var store = OrderDurableSubscribers.CreateStore();
        var checkpoints = new InMemoryDurableSubscriberCheckpointStore();
        GeneratedOrderDurableSubscriber.Reset();
        var subscriber = GeneratedOrderDurableSubscriber.Create(store, checkpoints);
        var index = 0;
        foreach (var shipmentEvent in events)
        {
            index++;
            _ = store.Append(Message<OrderShipmentEvent>.Create(shipmentEvent).WithMessageId($"shipment-{index}"));
        }

        var result = subscriber.CatchUp();
        return new(result.Completed, result.DeliveredCount, result.SkippedCount, result.LastSequence, GeneratedOrderDurableSubscriber.Entries);
    }
}

public static class OrderDurableSubscriberExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderDurableSubscriberDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => OrderDurableSubscribers.CreateStore());
        services.AddSingleton<IDurableSubscriberCheckpointStore, InMemoryDurableSubscriberCheckpointStore>();
        services.AddSingleton<OrderShipmentProjection>();
        services.AddSingleton(sp => OrderDurableSubscribers.Create(
            sp.GetRequiredService<MessageStore<OrderShipmentEvent>>(),
            sp.GetRequiredService<IDurableSubscriberCheckpointStore>(),
            sp.GetRequiredService<OrderShipmentProjection>()));
        services.AddSingleton<OrderDurableSubscriberService>();
        services.AddSingleton<OrderDurableSubscriberExampleRunner>();
        return services;
    }
}
