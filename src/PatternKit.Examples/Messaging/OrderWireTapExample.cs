using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

/// <summary>Order event observed by the wire-tap example.</summary>
public sealed record OrderWireTapEvent(string OrderId, string TenantId, decimal Total);

/// <summary>Summary returned by the order wire-tap example.</summary>
public sealed record OrderWireTapSummary(string OrderId, IReadOnlyList<string> InvokedTaps, IReadOnlyList<string> AuditTrail, IReadOnlyList<string> Metrics);

/// <summary>In-memory audit sink used by the example service.</summary>
public sealed class OrderWireTapAuditSink
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Record(OrderWireTapEvent evt, MessageContext context)
        => _entries.Add($"{context.Headers.CorrelationId ?? "uncorrelated"}:{evt.TenantId}:{evt.OrderId}");
}

/// <summary>In-memory metrics sink used by the example service.</summary>
public sealed class OrderWireTapMetricsSink
{
    private readonly List<string> _measurements = [];

    public IReadOnlyList<string> Measurements => _measurements;

    public void Record(OrderWireTapEvent evt)
        => _measurements.Add($"{evt.TenantId}:{evt.Total:0.00}");
}

/// <summary>Runtime sink resolver used by generated static tap handlers.</summary>
public static class OrderWireTapSinkRegistry
{
    public static OrderWireTapAuditSink Audit { get; set; } = new();

    public static OrderWireTapMetricsSink Metrics { get; set; } = new();
}

/// <summary>Fluent wire-tap builder used by non-generator consumers.</summary>
public static class OrderWireTaps
{
    public static WireTap<OrderWireTapEvent> Create(OrderWireTapAuditSink audit, OrderWireTapMetricsSink metrics)
        => WireTap<OrderWireTapEvent>.Create("order-observability")
            .AddTap("audit", (m, ctx) => audit.Record(m.Payload, ctx))
            .AddTap("metrics", (m, _) => metrics.Record(m.Payload))
            .Build();
}

/// <summary>Source-generated wire-tap handlers for order observability.</summary>
[GenerateWireTap(typeof(OrderWireTapEvent), FactoryName = "Create", TapName = "order-observability")]
public static partial class GeneratedOrderWireTap
{
    [WireTapHandler("audit", 10)]
    private static void Audit(Message<OrderWireTapEvent> message, MessageContext context)
        => OrderWireTapSinkRegistry.Audit.Record(message.Payload, context);

    [WireTapHandler("metrics", 20)]
    private static void Metrics(Message<OrderWireTapEvent> message, MessageContext context)
        => OrderWireTapSinkRegistry.Metrics.Record(message.Payload);
}

/// <summary>Service that publishes order events through a generated wire tap.</summary>
public sealed class OrderWireTapService(
    WireTap<OrderWireTapEvent> tap,
    OrderWireTapAuditSink audit,
    OrderWireTapMetricsSink metrics)
{
    public OrderWireTapSummary Publish(OrderWireTapEvent evt, string correlationId = "corr-order")
    {
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId(correlationId));
        var result = tap.Publish(Message<OrderWireTapEvent>.Create(evt), context);
        return new OrderWireTapSummary(evt.OrderId, result.InvokedTaps, audit.Entries, metrics.Measurements);
    }
}

/// <summary>Runner that demonstrates both fluent and generated wire-tap paths.</summary>
public sealed class OrderWireTapExampleRunner(OrderWireTapService service)
{
    public OrderWireTapSummary RunGenerated(OrderWireTapEvent evt) => service.Publish(evt);

    public static OrderWireTapSummary RunFluent(OrderWireTapEvent evt)
    {
        var audit = new OrderWireTapAuditSink();
        var metrics = new OrderWireTapMetricsSink();
        var tap = OrderWireTaps.Create(audit, metrics);
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("corr-order"));
        var result = tap.Publish(Message<OrderWireTapEvent>.Create(evt), context);
        return new OrderWireTapSummary(evt.OrderId, result.InvokedTaps, audit.Entries, metrics.Measurements);
    }
}

/// <summary>DI helpers for importing the order wire-tap example into standard .NET containers.</summary>
public static class OrderWireTapExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderWireTapDemo(this IServiceCollection services)
    {
        services.AddSingleton<OrderWireTapAuditSink>();
        services.AddSingleton<OrderWireTapMetricsSink>();
        services.AddSingleton(sp =>
        {
            OrderWireTapSinkRegistry.Audit = sp.GetRequiredService<OrderWireTapAuditSink>();
            OrderWireTapSinkRegistry.Metrics = sp.GetRequiredService<OrderWireTapMetricsSink>();
            return GeneratedOrderWireTap.Create();
        });
        services.AddSingleton<OrderWireTapService>();
        services.AddSingleton<OrderWireTapExampleRunner>();
        return services;
    }
}
