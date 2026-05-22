using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

public sealed record ShipmentEvent(long Sequence, string ShipmentId, string Status);

public sealed record ShipmentResequencerSummary(
    int ReleasedCount,
    string[] ReleasedStatuses,
    long NextExpectedSequence,
    bool Accepted,
    string? RejectionReason);

public sealed class ShipmentResequencerService(Resequencer<ShipmentEvent> resequencer)
{
    public ShipmentResequencerSummary Record(ShipmentEvent shipmentEvent)
    {
        var result = resequencer.Accept(Message<ShipmentEvent>.Create(shipmentEvent).WithCorrelationId(shipmentEvent.ShipmentId));
        return new(
            result.Released.Count,
            result.Released.Select(static item => item.Message.Payload.Status).ToArray(),
            result.NextExpectedSequence,
            result.Accepted,
            result.RejectionReason);
    }
}

public static class ShipmentResequencers
{
    public static Resequencer<ShipmentEvent> Create()
        => Resequencer<ShipmentEvent>.Create("shipment-events")
            .SelectSequence(static (message, _) => message.Payload.Sequence)
            .Build();
}

[GenerateResequencer(typeof(ShipmentEvent), FactoryName = "Create", Name = "shipment-events")]
public static partial class GeneratedShipmentResequencer
{
    [ResequencerSequence]
    private static long Select(Message<ShipmentEvent> message, MessageContext context) => message.Payload.Sequence;
}

public sealed class ShipmentResequencerExampleRunner(ShipmentResequencerService service)
{
    public ShipmentResequencerSummary RunGenerated(ShipmentEvent shipmentEvent) => service.Record(shipmentEvent);

    public static IReadOnlyList<ShipmentResequencerSummary> RunFluent(params ShipmentEvent[] events)
    {
        var service = new ShipmentResequencerService(ShipmentResequencers.Create());
        return events.Select(service.Record).ToArray();
    }
}

public static class ShipmentResequencerExampleServiceCollectionExtensions
{
    public static IServiceCollection AddShipmentResequencerDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedShipmentResequencer.Create());
        services.AddSingleton<ShipmentResequencerService>();
        services.AddSingleton<ShipmentResequencerExampleRunner>();
        return services;
    }
}
