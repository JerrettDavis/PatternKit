using Microsoft.Extensions.DependencyInjection;
using PatternKit.EnterpriseIntegration.EventNotification;
using PatternKit.Generators.EventNotification;

namespace PatternKit.Examples.EventNotificationDemo;

public sealed record OrderAcceptedNotificationEvent(string OrderId, string CorrelationId, string Source, bool NotifySubscribers);

public sealed record OrderNotificationSummary(string NotificationName, string OrderId, string CorrelationId, string Source);

public interface IOrderNotificationPublisher
{
    void Publish(OrderNotificationSummary notification);

    IReadOnlyList<OrderNotificationSummary> Published { get; }
}

public sealed class InMemoryOrderNotificationPublisher : IOrderNotificationPublisher
{
    private readonly List<OrderNotificationSummary> _published = [];

    public IReadOnlyList<OrderNotificationSummary> Published => _published;

    public void Publish(OrderNotificationSummary notification) => _published.Add(notification);
}

public sealed class OrderNotificationService(
    EventNotification<OrderAcceptedNotificationEvent, string> notification,
    IOrderNotificationPublisher publisher)
{
    public OrderNotificationSummary? Notify(OrderAcceptedNotificationEvent evt)
    {
        var result = notification.Notify(evt);
        if (result.Skipped)
            return null;
        if (result.Failed)
            throw new InvalidOperationException("Order notification could not be created.", result.Exception);

        var summary = new OrderNotificationSummary(result.NotificationName, result.Key!, result.CorrelationId, result.Metadata["source"]);
        publisher.Publish(summary);
        return summary;
    }
}

public static class OrderNotifications
{
    public static EventNotification<OrderAcceptedNotificationEvent, string> CreateFluent()
        => EventNotification<OrderAcceptedNotificationEvent, string>.Create("order-accepted")
            .When(static evt => evt.NotifySubscribers)
            .WithKey(static evt => evt.OrderId)
            .WithCorrelation(static evt => evt.CorrelationId)
            .WithMetadata("source", static evt => evt.Source)
            .Build();
}

[GenerateEventNotification(typeof(OrderAcceptedNotificationEvent), typeof(string), FactoryMethodName = "Create", NotificationName = "order-accepted")]
public static partial class GeneratedOrderAcceptedNotification
{
    [EventNotificationRule]
    private static bool ShouldNotify(OrderAcceptedNotificationEvent evt) => evt.NotifySubscribers;

    [EventNotificationKey]
    private static string Key(OrderAcceptedNotificationEvent evt) => evt.OrderId;

    [EventNotificationCorrelation]
    private static string Correlation(OrderAcceptedNotificationEvent evt) => evt.CorrelationId;

    [EventNotificationMetadata("source")]
    private static string Source(OrderAcceptedNotificationEvent evt) => evt.Source;
}

public sealed class OrderEventNotificationDemoRunner(OrderNotificationService service)
{
    public OrderNotificationSummary? RunGenerated(OrderAcceptedNotificationEvent evt) => service.Notify(evt);

    public static OrderNotificationSummary? RunFluent()
    {
        var service = new OrderNotificationService(OrderNotifications.CreateFluent(), new InMemoryOrderNotificationPublisher());
        return service.Notify(new OrderAcceptedNotificationEvent("O-100", "C-900", "web", true));
    }
}

public static class OrderEventNotificationServiceCollectionExtensions
{
    public static IServiceCollection AddOrderEventNotificationDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedOrderAcceptedNotification.Create());
        services.AddSingleton<IOrderNotificationPublisher, InMemoryOrderNotificationPublisher>();
        services.AddSingleton<OrderNotificationService>();
        services.AddSingleton<OrderEventNotificationDemoRunner>();
        return services;
    }
}
