using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Diagnostics;

namespace PatternKit.Examples.Messaging;

public sealed record HistoryOrder(string Id, decimal Total, string Channel);

public sealed record OrderMessageHistorySummary(string OrderId, int HistoryCount, IReadOnlyList<string> Components, string? CorrelationId);

public sealed class OrderMessageHistoryService(MessageHistory<HistoryOrder> received, MessageHistory<HistoryOrder> routed)
{
    public OrderMessageHistorySummary Capture(HistoryOrder order)
    {
        var message = Message<HistoryOrder>.Create(order).WithCorrelationId($"order:{order.Id}");
        var result = routed.Record(received.Record(message));
        var history = MessageHistory<HistoryOrder>.Read(result);

        return new(
            result.Payload.Id,
            history.Count,
            history.Select(static entry => entry.Component).ToArray(),
            result.Headers.CorrelationId);
    }
}

public static class OrderMessageHistories
{
    public static MessageHistory<HistoryOrder> CreateReceived()
        => MessageHistory<HistoryOrder>.Create("checkout-api")
            .Action("received")
            .Details(static message => message.Payload.Channel)
            .Build();

    public static MessageHistory<HistoryOrder> CreateRouted()
        => MessageHistory<HistoryOrder>.Create("fulfillment-router")
            .Action("routed")
            .Build();
}

[GenerateMessageHistory(typeof(HistoryOrder), "checkout-api", FactoryName = "Create", Action = "received")]
public static partial class GeneratedOrderReceivedHistory;

[GenerateMessageHistory(typeof(HistoryOrder), "fulfillment-router", FactoryName = "Create", Action = "routed")]
public static partial class GeneratedOrderRoutedHistory;

public sealed class OrderMessageHistoryExampleRunner(OrderMessageHistoryService service)
{
    public OrderMessageHistorySummary RunGenerated(HistoryOrder order)
        => service.Capture(order);

    public static OrderMessageHistorySummary RunFluent(HistoryOrder order)
        => new OrderMessageHistoryService(
            OrderMessageHistories.CreateReceived(),
            OrderMessageHistories.CreateRouted()).Capture(order);

    public static OrderMessageHistorySummary RunGeneratedStatic(HistoryOrder order)
        => new OrderMessageHistoryService(
            GeneratedOrderReceivedHistory.Create()
                .Details(static message => message.Payload.Channel)
                .Build(),
            GeneratedOrderRoutedHistory.Create().Build()).Capture(order);
}

public static class OrderMessageHistoryServiceCollectionExtensions
{
    public static IServiceCollection AddOrderMessageHistoryDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedOrderReceivedHistory.Create()
            .Details(static message => message.Payload.Channel)
            .Build());
        services.AddSingleton(static _ => GeneratedOrderRoutedHistory.Create().Build());
        services.AddSingleton(static sp =>
        {
            var histories = sp.GetServices<MessageHistory<HistoryOrder>>().ToArray();
            return new OrderMessageHistoryService(histories[0], histories[1]);
        });
        services.AddSingleton<OrderMessageHistoryExampleRunner>();
        return services;
    }
}
