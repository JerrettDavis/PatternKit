using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Examples.Messaging;

/// <summary>Shipment event that must survive transient worker failures.</summary>
public sealed record ShipmentDispatchCommand(string ShipmentId, string Carrier, string Destination);

/// <summary>Summary returned by the guaranteed-delivery example.</summary>
public sealed record ShipmentDeliverySummary(string ShipmentId, GuaranteedDeliveryStatus Status, int Attempts, string? LastError);

/// <summary>Application service that uses a guaranteed-delivery queue for shipment dispatch work.</summary>
public sealed class ShipmentGuaranteedDeliveryService(GuaranteedDeliveryQueue<ShipmentDispatchCommand> queue)
{
    public async ValueTask<GuaranteedDeliveryRecord<ShipmentDispatchCommand>> ScheduleAsync(
        ShipmentDispatchCommand command,
        CancellationToken cancellationToken = default)
        => await queue.EnqueueAsync(
            Message<ShipmentDispatchCommand>.Create(command),
            $"shipment:{command.ShipmentId}",
            cancellationToken: cancellationToken).ConfigureAwait(false);

    public async ValueTask<ShipmentDeliverySummary> DispatchNextAsync(
        bool transientFailure = false,
        CancellationToken cancellationToken = default)
    {
        var lease = await queue.TryReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (lease is null)
            return new ShipmentDeliverySummary(string.Empty, GuaranteedDeliveryStatus.Pending, 0, null);

        if (transientFailure)
        {
            await queue.ReleaseAsync(lease, "carrier API unavailable", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await queue.AcknowledgeAsync(lease, cancellationToken).ConfigureAwait(false);
        }

        var record = (await queue.SnapshotAsync(cancellationToken).ConfigureAwait(false))
            .Single(item => item.Id == lease.Id);
        return new ShipmentDeliverySummary(record.Message.Payload.ShipmentId, record.Status, record.Attempts, record.LastError);
    }
}

/// <summary>Fluent guaranteed-delivery queue builder for shipment dispatch work.</summary>
public static class ShipmentGuaranteedDeliveryQueues
{
    public static GuaranteedDeliveryQueue<ShipmentDispatchCommand> Create()
        => GuaranteedDeliveryQueue<ShipmentDispatchCommand>.Create(new InMemoryGuaranteedDeliveryStore<ShipmentDispatchCommand>())
            .Name("shipment-guaranteed-delivery")
            .LeaseDuration(TimeSpan.FromSeconds(30))
            .MaxDeliveryAttempts(3)
            .Build();
}

/// <summary>Source-generated guaranteed-delivery queue for shipment dispatch work.</summary>
[GenerateGuaranteedDelivery(
    typeof(ShipmentDispatchCommand),
    FactoryName = "Create",
    QueueName = "shipment-guaranteed-delivery",
    LeaseMilliseconds = 30000,
    MaxDeliveryAttempts = 3)]
public static partial class GeneratedShipmentGuaranteedDeliveryQueue;

/// <summary>Runner that demonstrates fluent and generated guaranteed-delivery paths.</summary>
public sealed class ShipmentGuaranteedDeliveryExampleRunner(ShipmentGuaranteedDeliveryService service)
{
    public async ValueTask<ShipmentDeliverySummary> RunGeneratedAsync(
        ShipmentDispatchCommand command,
        CancellationToken cancellationToken = default)
    {
        await service.ScheduleAsync(command, cancellationToken).ConfigureAwait(false);
        return await service.DispatchNextAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<ShipmentDeliverySummary> RunFluentAsync(
        ShipmentDispatchCommand command,
        bool transientFailure = false,
        CancellationToken cancellationToken = default)
    {
        var service = new ShipmentGuaranteedDeliveryService(ShipmentGuaranteedDeliveryQueues.Create());
        await service.ScheduleAsync(command, cancellationToken).ConfigureAwait(false);
        return await service.DispatchNextAsync(transientFailure, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>DI helpers for importing the shipment guaranteed-delivery example.</summary>
public static class ShipmentGuaranteedDeliveryExampleServiceCollectionExtensions
{
    public static IServiceCollection AddShipmentGuaranteedDeliveryDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedShipmentGuaranteedDeliveryQueue.Create());
        services.AddSingleton<ShipmentGuaranteedDeliveryService>();
        services.AddSingleton<ShipmentGuaranteedDeliveryExampleRunner>();
        return services;
    }
}
