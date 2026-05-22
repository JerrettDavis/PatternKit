using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Consumers;
using System.Globalization;

namespace PatternKit.Examples.Messaging;

public sealed record OrderAcceptedEvent(string OrderId, decimal Total);

public sealed record OrderEventDrivenSummary(bool Accepted, int HandlerCount, IReadOnlyList<string> AuditEntries);

public sealed class OrderEventDrivenAuditSink
{
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries.AsReadOnly();

    public void Append(string entry) => _entries.Add(entry);

    public void Clear() => _entries.Clear();
}

public sealed class OrderEventDrivenConsumerService(EventDrivenConsumer<OrderAcceptedEvent> consumer, OrderEventDrivenAuditSink sink)
{
    public OrderEventDrivenSummary Accept(OrderAcceptedEvent accepted)
    {
        var result = consumer.Accept(Message<OrderAcceptedEvent>.Create(accepted));
        return new(result.Accepted, result.HandlerCount, sink.Entries);
    }
}

public static class OrderEventDrivenConsumers
{
    public static EventDrivenConsumer<OrderAcceptedEvent> Create(OrderEventDrivenAuditSink sink)
        => EventDrivenConsumer<OrderAcceptedEvent>.Create("order-accepted-consumer")
            .Handle("audit", (message, _) =>
            {
                sink.Append(ToAuditEntry(message.Payload));
                return EventDrivenConsumerHandlerResult.Success("audit");
            })
            .Build();

    public static string ToAuditEntry(OrderAcceptedEvent accepted)
        => $"accepted:{accepted.OrderId}:{accepted.Total.ToString("0.00", CultureInfo.InvariantCulture)}";
}

[GenerateEventDrivenConsumer(typeof(OrderAcceptedEvent), FactoryName = "Create", ConsumerName = "order-accepted-consumer")]
public static partial class GeneratedOrderEventDrivenConsumer
{
    private static readonly OrderEventDrivenAuditSink Sink = new();

    public static IReadOnlyList<string> Entries => Sink.Entries;

    public static void Reset() => Sink.Clear();

    [EventDrivenConsumerHandler("audit")]
    private static EventDrivenConsumerHandlerResult Audit(Message<OrderAcceptedEvent> message, MessageContext context)
    {
        Sink.Append(OrderEventDrivenConsumers.ToAuditEntry(message.Payload));
        return EventDrivenConsumerHandlerResult.Success("audit");
    }
}

public sealed class OrderEventDrivenConsumerExampleRunner(OrderEventDrivenConsumerService service)
{
    public OrderEventDrivenSummary RunGenerated(OrderAcceptedEvent accepted) => service.Accept(accepted);

    public static OrderEventDrivenSummary RunGeneratedStatic(OrderAcceptedEvent accepted)
    {
        GeneratedOrderEventDrivenConsumer.Reset();
        var result = GeneratedOrderEventDrivenConsumer.Create().Accept(Message<OrderAcceptedEvent>.Create(accepted));
        return new(result.Accepted, result.HandlerCount, GeneratedOrderEventDrivenConsumer.Entries);
    }

    public static OrderEventDrivenSummary RunFluent(OrderAcceptedEvent accepted)
    {
        var sink = new OrderEventDrivenAuditSink();
        return new OrderEventDrivenConsumerService(OrderEventDrivenConsumers.Create(sink), sink).Accept(accepted);
    }
}

public static class OrderEventDrivenConsumerExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderEventDrivenConsumerDemo(this IServiceCollection services)
    {
        services.AddSingleton<OrderEventDrivenAuditSink>();
        services.AddSingleton(sp => OrderEventDrivenConsumers.Create(sp.GetRequiredService<OrderEventDrivenAuditSink>()));
        services.AddSingleton<OrderEventDrivenConsumerService>();
        services.AddSingleton<OrderEventDrivenConsumerExampleRunner>();
        return services;
    }
}
