using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.EventualConsistency;
using PatternKit.Generators.EventualConsistency;

namespace PatternKit.Examples.EventualConsistencyDemo;

public static class OrderProjectionConsistencyDemo
{
    public static OrderProjectionConsistencySummary RunFluent()
    {
        var monitor = OrderProjectionConsistencyPolicies.CreateFluentMonitor();
        return RunScenario(monitor, "order-100");
    }

    public static OrderProjectionConsistencySummary RunGenerated()
    {
        var monitor = GeneratedOrderProjectionConsistencyMonitor.CreateMonitor();
        return RunScenario(monitor, "order-200");
    }

    private static OrderProjectionConsistencySummary RunScenario(EventualConsistencyMonitor<string> monitor, string orderId)
    {
        monitor.RecordSource(orderId, 10, $"events:{orderId}");
        var lagging = monitor.RecordTarget(orderId, 7, $"projection:{orderId}");
        var converged = monitor.RecordTarget(orderId, 9, $"projection:{orderId}");
        return new OrderProjectionConsistencySummary(
            monitor.Name,
            orderId,
            lagging.Status,
            converged.Status,
            converged.Lag,
            converged.IsConverged);
    }
}

public sealed record OrderProjectionConsistencySummary(
    string MonitorName,
    string OrderId,
    EventualConsistencyStatus InitialStatus,
    EventualConsistencyStatus FinalStatus,
    long FinalLag,
    bool Converged);

public static class OrderProjectionConsistencyPolicies
{
    public static EventualConsistencyMonitor<string> CreateFluentMonitor()
        => EventualConsistencyMonitor<string>
            .Create("order-projection-consistency")
            .UseComparer(StringComparer.OrdinalIgnoreCase)
            .WithMaxAllowedLag(1)
            .Build();
}

public sealed class OrderProjectionConsistencyService
{
    private readonly EventualConsistencyMonitor<string> _monitor;

    public OrderProjectionConsistencyService(EventualConsistencyMonitor<string> monitor)
    {
        _monitor = monitor;
    }

    public OrderProjectionConsistencySummary RecordProjectionProgress(string orderId, long sourceVersion, long projectionVersion)
    {
        _monitor.RecordSource(orderId, sourceVersion, $"source:{orderId}");
        var evaluation = _monitor.RecordTarget(orderId, projectionVersion, $"projection:{orderId}");
        return new OrderProjectionConsistencySummary(
            _monitor.Name,
            orderId,
            evaluation.Status,
            evaluation.Status,
            evaluation.Lag,
            evaluation.IsConverged);
    }
}

public sealed record OrderProjectionConsistencyDemoRunner(
    Func<OrderProjectionConsistencySummary> RunFluent,
    Func<OrderProjectionConsistencySummary> RunGenerated);

public static class OrderProjectionConsistencyServiceCollectionExtensions
{
    public static IServiceCollection AddOrderProjectionConsistencyDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => OrderProjectionConsistencyPolicies.CreateFluentMonitor());
        services.AddTransient<OrderProjectionConsistencyService>();
        services.AddSingleton(new OrderProjectionConsistencyDemoRunner(
            OrderProjectionConsistencyDemo.RunFluent,
            OrderProjectionConsistencyDemo.RunGenerated));
        return services;
    }
}

[GenerateEventualConsistencyMonitor(typeof(string), FactoryMethodName = "CreateMonitor", MonitorName = "order-projection-consistency", MaxAllowedLag = 1)]
public static partial class GeneratedOrderProjectionConsistencyMonitor;
