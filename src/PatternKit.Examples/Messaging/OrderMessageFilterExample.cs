using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

/// <summary>Order intake command used by the message-filter example.</summary>
public sealed record OrderMessageFilterCommand(string OrderId, string CustomerTier, decimal Total, bool PaymentVerified);

/// <summary>Summary returned by the order message-filter example.</summary>
public sealed record OrderMessageFilterSummary(bool Accepted, string? RuleName, string? RejectionReason);

/// <summary>
/// Service that applies an importable message filter before an order moves into fulfillment.
/// </summary>
public sealed class OrderMessageFilterService(MessageFilter<OrderMessageFilterCommand> filter)
{
    public OrderMessageFilterSummary Screen(OrderMessageFilterCommand command)
    {
        var result = filter.Filter(Message<OrderMessageFilterCommand>.Create(command));
        return new OrderMessageFilterSummary(result.Accepted, result.RuleName, result.RejectionReason);
    }
}

/// <summary>Fluent message-filter builder used by applications that do not enable generators.</summary>
public static class OrderMessageFilters
{
    public static MessageFilter<OrderMessageFilterCommand> CreateFraudScreen()
        => MessageFilter<OrderMessageFilterCommand>.Create("order-fraud-screen")
            .AllowWhen("trusted-customer", static (m, _) => m.Payload.CustomerTier == "trusted" && m.Payload.PaymentVerified)
            .AllowWhen("verified-low-value", static (m, _) => m.Payload.PaymentVerified && m.Payload.Total <= 100m)
            .RejectUnmatched("Order requires fraud review before fulfillment.")
            .Build();
}

/// <summary>Source-generated message-filter rules for order fraud screening.</summary>
[GenerateMessageFilter(
    typeof(OrderMessageFilterCommand),
    FactoryName = "Create",
    FilterName = "order-fraud-screen",
    RejectionReason = "Order requires fraud review before fulfillment.")]
public static partial class GeneratedOrderMessageFilter
{
    [MessageFilterRule("trusted-customer", 10)]
    private static bool IsTrustedCustomer(Message<OrderMessageFilterCommand> message, MessageContext context)
        => message.Payload.CustomerTier == "trusted" && message.Payload.PaymentVerified;

    [MessageFilterRule("verified-low-value", 20)]
    private static bool IsVerifiedLowValue(Message<OrderMessageFilterCommand> message, MessageContext context)
        => message.Payload.PaymentVerified && message.Payload.Total <= 100m;
}

/// <summary>Runner that demonstrates both fluent and generated message-filter paths.</summary>
public sealed class OrderMessageFilterExampleRunner(OrderMessageFilterService service)
{
    public OrderMessageFilterSummary RunGenerated(OrderMessageFilterCommand command) => service.Screen(command);

    public static OrderMessageFilterSummary RunFluent(OrderMessageFilterCommand command)
    {
        var result = OrderMessageFilters.CreateFraudScreen().Filter(Message<OrderMessageFilterCommand>.Create(command));
        return new OrderMessageFilterSummary(result.Accepted, result.RuleName, result.RejectionReason);
    }
}

/// <summary>DI helpers for importing the order message-filter example into standard .NET containers.</summary>
public static class OrderMessageFilterExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderMessageFilterDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedOrderMessageFilter.Create());
        services.AddSingleton<OrderMessageFilterService>();
        services.AddSingleton<OrderMessageFilterExampleRunner>();
        return services;
    }
}
