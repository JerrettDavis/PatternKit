using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.MaterializedViews;
using PatternKit.Generators.MaterializedViews;

namespace PatternKit.Examples.MaterializedViewDemo;

public static class OrderMaterializedViewDemo
{
    public static async ValueTask<OrderMaterializedViewSummary> RunFluentAsync()
        => await RunScenarioAsync(OrderMaterializedViewPolicies.CreateFluentView(), "order-100");

    public static async ValueTask<OrderMaterializedViewSummary> RunGeneratedAsync()
        => await RunScenarioAsync(GeneratedOrderMaterializedView.CreateView(), "order-200");

    private static async ValueTask<OrderMaterializedViewSummary> RunScenarioAsync(
        IMaterializedView<OrderReadModel, OrderReadModelEvent> view,
        string orderId)
    {
        var events = new OrderReadModelEvent[]
        {
            new OrderPlacedForReadModel(orderId, "customer-1", 125m, DateTimeOffset.UtcNow),
            new OrderPaymentCapturedForReadModel(orderId, "payment-1", DateTimeOffset.UtcNow),
            new OrderShippedForReadModel(orderId, "tracking-1", DateTimeOffset.UtcNow)
        };

        var projected = await view.ProjectAsync(OrderReadModel.Empty(view.Name), events).ConfigureAwait(false);
        return new OrderMaterializedViewSummary(
            projected.ViewName,
            projected.OrderId,
            projected.CustomerId,
            projected.Total,
            projected.Status,
            projected.LastUpdatedAt,
            projected.Changes.Count);
    }
}

public abstract record OrderReadModelEvent(string OrderId, DateTimeOffset OccurredAt);

public sealed record OrderPlacedForReadModel(string OrderId, string CustomerId, decimal Total, DateTimeOffset OccurredAt)
    : OrderReadModelEvent(OrderId, OccurredAt);

public sealed record OrderPaymentCapturedForReadModel(string OrderId, string PaymentId, DateTimeOffset OccurredAt)
    : OrderReadModelEvent(OrderId, OccurredAt);

public sealed record OrderShippedForReadModel(string OrderId, string TrackingNumber, DateTimeOffset OccurredAt)
    : OrderReadModelEvent(OrderId, OccurredAt);

public sealed record OrderReadModel(
    string ViewName,
    string OrderId,
    string CustomerId,
    decimal Total,
    string Status,
    DateTimeOffset LastUpdatedAt,
    IReadOnlyList<string> Changes)
{
    public static OrderReadModel Empty(string viewName)
        => new(viewName, "", "", 0m, "Pending", DateTimeOffset.MinValue, Array.Empty<string>());
}

public sealed record OrderMaterializedViewSummary(
    string ViewName,
    string OrderId,
    string CustomerId,
    decimal Total,
    string Status,
    DateTimeOffset LastUpdatedAt,
    int ChangeCount);

public static class OrderMaterializedViewPolicies
{
    public static MaterializedView<OrderReadModel, OrderReadModelEvent> CreateFluentView()
        => MaterializedView<OrderReadModel, OrderReadModelEvent>.Create("order-read-model")
            .WithHandler<OrderPlacedForReadModel>(ApplyOrderPlaced)
            .WithHandler<OrderPaymentCapturedForReadModel>(ApplyPaymentCaptured)
            .WithHandler<OrderShippedForReadModel>(ApplyOrderShipped)
            .Build();

    public static OrderReadModel ApplyOrderPlaced(OrderReadModel state, OrderPlacedForReadModel @event)
        => state with
        {
            OrderId = @event.OrderId,
            CustomerId = @event.CustomerId,
            Total = @event.Total,
            Status = "Placed",
            LastUpdatedAt = @event.OccurredAt,
            Changes = Append(state.Changes, "placed")
        };

    public static OrderReadModel ApplyPaymentCaptured(OrderReadModel state, OrderPaymentCapturedForReadModel @event)
        => state with
        {
            OrderId = @event.OrderId,
            Status = "Paid",
            LastUpdatedAt = @event.OccurredAt,
            Changes = Append(state.Changes, "paid")
        };

    public static OrderReadModel ApplyOrderShipped(OrderReadModel state, OrderShippedForReadModel @event)
        => state with
        {
            OrderId = @event.OrderId,
            Status = "Shipped",
            LastUpdatedAt = @event.OccurredAt,
            Changes = Append(state.Changes, "shipped")
        };

    private static IReadOnlyList<string> Append(IReadOnlyList<string> values, string value)
    {
        var next = values.ToList();
        next.Add(value);
        return next;
    }
}

public sealed class OrderMaterializedViewWorkflow
{
    private readonly IMaterializedView<OrderReadModel, OrderReadModelEvent> _view;

    public OrderMaterializedViewWorkflow(IMaterializedView<OrderReadModel, OrderReadModelEvent> view)
    {
        _view = view;
    }

    public async ValueTask<OrderMaterializedViewSummary> BuildReadModelAsync(
        IReadOnlyList<OrderReadModelEvent> events,
        CancellationToken cancellationToken = default)
    {
        var projected = await _view.ProjectAsync(OrderReadModel.Empty(_view.Name), events, cancellationToken).ConfigureAwait(false);
        return new OrderMaterializedViewSummary(
            projected.ViewName,
            projected.OrderId,
            projected.CustomerId,
            projected.Total,
            projected.Status,
            projected.LastUpdatedAt,
            projected.Changes.Count);
    }
}

public sealed record OrderMaterializedViewDemoRunner(
    Func<ValueTask<OrderMaterializedViewSummary>> RunFluentAsync,
    Func<ValueTask<OrderMaterializedViewSummary>> RunGeneratedAsync);

public static class OrderMaterializedViewServiceCollectionExtensions
{
    public static IServiceCollection AddOrderMaterializedViewDemo(this IServiceCollection services)
    {
        services.AddScoped<IMaterializedView<OrderReadModel, OrderReadModelEvent>>(_ => OrderMaterializedViewPolicies.CreateFluentView());
        services.AddScoped<OrderMaterializedViewWorkflow>();
        services.AddSingleton(new OrderMaterializedViewDemoRunner(
            OrderMaterializedViewDemo.RunFluentAsync,
            OrderMaterializedViewDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateMaterializedView(typeof(OrderReadModel), typeof(OrderReadModelEvent), FactoryName = "CreateView", ViewName = "order-read-model")]
public static partial class GeneratedOrderMaterializedView
{
    [MaterializedViewHandler(typeof(OrderPlacedForReadModel), Order = 10)]
    private static OrderReadModel ApplyOrderPlaced(OrderReadModel state, OrderPlacedForReadModel @event)
        => OrderMaterializedViewPolicies.ApplyOrderPlaced(state, @event);

    [MaterializedViewHandler(typeof(OrderPaymentCapturedForReadModel), Order = 20)]
    private static OrderReadModel ApplyPaymentCaptured(OrderReadModel state, OrderPaymentCapturedForReadModel @event)
        => OrderMaterializedViewPolicies.ApplyPaymentCaptured(state, @event);

    [MaterializedViewHandler(typeof(OrderShippedForReadModel), Order = 30)]
    private static OrderReadModel ApplyOrderShipped(OrderReadModel state, OrderShippedForReadModel @event)
        => OrderMaterializedViewPolicies.ApplyOrderShipped(state, @event);
}
