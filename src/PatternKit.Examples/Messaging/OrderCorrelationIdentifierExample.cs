using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Correlation;

namespace PatternKit.Examples.Messaging;

public sealed record CorrelatedOrder(string OrderId, string CustomerId, decimal Total);

public sealed record CorrelatedOrderAccepted(string OrderId, bool Accepted);

public sealed record OrderCorrelationSummary(string RequestCorrelationId, string ReplyCorrelationId, string CustomHeaderCorrelationId);

public sealed class OrderCorrelationService
{
    private readonly CorrelationIdentifier<CorrelatedOrder> _correlation;

    public OrderCorrelationService(CorrelationIdentifier<CorrelatedOrder> correlation)
    {
        _correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
    }

    public OrderCorrelationSummary Accept(CorrelatedOrder order)
    {
        var request = _correlation.Ensure(Message<CorrelatedOrder>.Create(order).WithMessageId("msg-" + order.OrderId));
        var reply = _correlation.CorrelateReply(Message<CorrelatedOrderAccepted>.Create(new(order.OrderId, true)), request);
        var custom = GeneratedOrderCorrelation.Create()
            .Select(static (message, _) => "customer:" + message.Payload.CustomerId)
            .Build()
            .Ensure(Message<CorrelatedOrder>.Create(order));

        return new OrderCorrelationSummary(
            request.Headers.GetString(_correlation.HeaderName) ?? string.Empty,
            reply.Headers.GetString(_correlation.HeaderName) ?? string.Empty,
            custom.Headers.GetString("X-Correlation") ?? string.Empty);
    }
}

public static class OrderCorrelationIdentifiers
{
    public static CorrelationIdentifier<CorrelatedOrder>.Builder Create()
        => CorrelationIdentifier<CorrelatedOrder>.Create()
            .Select(static (message, _) => "order:" + message.Payload.OrderId);
}

[GenerateCorrelationIdentifier(typeof(CorrelatedOrder), FactoryName = "Create", HeaderName = "X-Correlation")]
public static partial class GeneratedOrderCorrelation;

public static class OrderCorrelationIdentifierExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderCorrelationIdentifierDemo(this IServiceCollection services)
    {
        services.AddSingleton(OrderCorrelationIdentifiers.Create().Build());
        services.AddSingleton<OrderCorrelationService>();
        services.AddSingleton(new OrderCorrelationIdentifierExampleRunner(
            RunFluent: static () => new OrderCorrelationService(OrderCorrelationIdentifiers.Create().Build())
                .Accept(new("ord-100", "cust-7", 42.50m)),
            RunGenerated: static () =>
            {
                var correlation = GeneratedOrderCorrelation.Create()
                    .Select(static (message, _) => "customer:" + message.Payload.CustomerId)
                    .Build();
                return new OrderCorrelationService(correlation)
                    .Accept(new("ord-100", "cust-7", 42.50m));
            }));
        return services;
    }
}

public sealed record OrderCorrelationIdentifierExampleRunner(
    Func<OrderCorrelationSummary> RunFluent,
    Func<OrderCorrelationSummary> RunGenerated)
{
    public static OrderCorrelationSummary RunFluentStatic(CorrelatedOrder order)
        => new OrderCorrelationService(OrderCorrelationIdentifiers.Create().Build()).Accept(order);

    public static OrderCorrelationSummary RunGeneratedStatic(CorrelatedOrder order)
    {
        var correlation = GeneratedOrderCorrelation.Create()
            .Select(static (message, _) => "customer:" + message.Payload.CustomerId)
            .Build();
        return new OrderCorrelationService(correlation).Accept(order);
    }
}
