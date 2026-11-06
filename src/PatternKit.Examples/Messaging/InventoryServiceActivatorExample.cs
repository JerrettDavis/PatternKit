using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Activation;

namespace PatternKit.Examples.Messaging;

public sealed record InventoryReservationRequest(string Sku, int Quantity);

public sealed record InventoryReservationResult(string Sku, bool Reserved, string Reason);

public sealed record InventoryServiceActivatorSummary(bool Completed, bool Reserved, string Reason);

public sealed class InventoryServiceActivatorService(ServiceActivator<InventoryReservationRequest, InventoryReservationResult> activator)
{
    public InventoryServiceActivatorSummary Reserve(InventoryReservationRequest request)
    {
        var result = activator.Activate(Message<InventoryReservationRequest>.Create(request));
        return new(result.Completed, result.Response.Payload.Reserved, result.Response.Payload.Reason);
    }
}

public static class InventoryServiceActivators
{
    public static ServiceActivator<InventoryReservationRequest, InventoryReservationResult> Create()
        => ServiceActivator<InventoryReservationRequest, InventoryReservationResult>.Create("inventory-reservation-activator")
            .Handle(Reserve)
            .Build();

    public static Message<InventoryReservationResult> Reserve(Message<InventoryReservationRequest> request, MessageContext context)
    {
        var reserved = request.Payload.Quantity <= 25;
        var reason = reserved ? "allocated" : "insufficient-stock";
        return Message<InventoryReservationResult>.Create(new(request.Payload.Sku, reserved, reason));
    }
}

[GenerateServiceActivator(typeof(InventoryReservationRequest), typeof(InventoryReservationResult), FactoryName = "Create", ActivatorName = "inventory-reservation-activator")]
public static partial class GeneratedInventoryServiceActivator
{
    [ServiceActivatorHandler]
    private static Message<InventoryReservationResult> Reserve(Message<InventoryReservationRequest> request, MessageContext context)
        => InventoryServiceActivators.Reserve(request, context);
}

public sealed class InventoryServiceActivatorExampleRunner(InventoryServiceActivatorService service)
{
    public InventoryServiceActivatorSummary RunGenerated(InventoryReservationRequest request) => service.Reserve(request);

    public static InventoryServiceActivatorSummary RunFluent(InventoryReservationRequest request)
        => new InventoryServiceActivatorService(InventoryServiceActivators.Create()).Reserve(request);

    public static InventoryServiceActivatorSummary RunGeneratedStatic(InventoryReservationRequest request)
        => new InventoryServiceActivatorService(GeneratedInventoryServiceActivator.Create()).Reserve(request);
}

public static class InventoryServiceActivatorExampleServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryServiceActivatorDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedInventoryServiceActivator.Create());
        services.AddSingleton<InventoryServiceActivatorService>();
        services.AddSingleton<InventoryServiceActivatorExampleRunner>();
        return services;
    }
}
