using BenchmarkDotNet.Attributes;
using PatternKit.EnterpriseIntegration.EventNotification;
using PatternKit.Examples.EventNotificationDemo;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "EventNotification")]
public class EventNotificationBenchmarks
{
    private static readonly OrderAcceptedNotificationEvent Event = new("O-100", "C-900", "web", true);

    [Benchmark(Baseline = true, Description = "Fluent: create event notification")]
    [BenchmarkCategory("Fluent", "Construction")]
    public EventNotification<OrderAcceptedNotificationEvent, string> Fluent_CreateEventNotification()
        => OrderNotifications.CreateFluent();

    [Benchmark(Description = "Generated: create event notification")]
    [BenchmarkCategory("Generated", "Construction")]
    public EventNotification<OrderAcceptedNotificationEvent, string> Generated_CreateEventNotification()
        => GeneratedOrderAcceptedNotification.Create();

    [Benchmark(Description = "Fluent: publish order notification")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderNotificationSummary? Fluent_PublishOrderNotification()
        => new OrderNotificationService(OrderNotifications.CreateFluent(), new InMemoryOrderNotificationPublisher()).Notify(Event);

    [Benchmark(Description = "Generated: publish order notification")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderNotificationSummary? Generated_PublishOrderNotification()
        => new OrderNotificationService(GeneratedOrderAcceptedNotification.Create(), new InMemoryOrderNotificationPublisher()).Notify(Event);
}
