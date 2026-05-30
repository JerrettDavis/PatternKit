using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.Timeouts;
using PatternKit.Generators.Timeouts;

namespace PatternKit.Examples.TimeoutManagerDemo;

public sealed record OrderReservationRequest(Guid OrderId, string RequestId, TimeSpan HoldFor);

public sealed record OrderReservationTimeoutSummary(int PendingReservations, IReadOnlyList<Guid> ExpiredOrders);

public static partial class OrderReservationTimeoutManagers
{
    public static TimeoutManager<Guid> CreateFluent()
        => TimeoutManager<Guid>.Create("order-reservation-timeouts").Build();
}

[GenerateTimeoutManager(typeof(Guid), FactoryMethodName = "CreateGenerated", ManagerName = "order-reservation-timeouts")]
public static partial class GeneratedOrderReservationTimeoutManager;

public sealed class OrderReservationTimeoutService(TimeoutManager<Guid> timeouts)
{
    public TimeoutRecord<Guid> Reserve(OrderReservationRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return timeouts.ScheduleAfter(request.OrderId, request.HoldFor, request.RequestId);
    }

    public bool Complete(Guid orderId) => timeouts.Complete(orderId);

    public OrderReservationTimeoutSummary ExpireDue(DateTimeOffset now)
    {
        var expired = timeouts.ExpireDue(now);
        return new(timeouts.PendingCount, expired.Select(static timeout => timeout.Key).ToArray());
    }
}

public sealed class OrderReservationTimeoutDemoRunner(OrderReservationTimeoutService service)
{
    public OrderReservationTimeoutSummary RunGenerated(OrderReservationRequest request, DateTimeOffset expiresAt)
    {
        service.Reserve(request);
        return service.ExpireDue(expiresAt);
    }

    public static OrderReservationTimeoutSummary RunFluent(OrderReservationRequest request, DateTimeOffset expiresAt)
    {
        var service = new OrderReservationTimeoutService(OrderReservationTimeoutManagers.CreateFluent());
        service.Reserve(request);
        return service.ExpireDue(expiresAt);
    }

    public static OrderReservationTimeoutSummary RunGeneratedStatic(OrderReservationRequest request, DateTimeOffset expiresAt)
    {
        var service = new OrderReservationTimeoutService(GeneratedOrderReservationTimeoutManager.CreateGenerated());
        service.Reserve(request);
        return service.ExpireDue(expiresAt);
    }
}

public static class OrderReservationTimeoutDemoServiceCollectionExtensions
{
    public static IServiceCollection AddOrderReservationTimeoutDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedOrderReservationTimeoutManager.CreateGenerated());
        services.AddSingleton<OrderReservationTimeoutService>();
        services.AddSingleton<OrderReservationTimeoutDemoRunner>();
        return services;
    }
}
