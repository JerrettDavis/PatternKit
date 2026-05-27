using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Examples.Messaging;

/// <summary>Order work item used by the message-expiration example.</summary>
public sealed record ExpiringOrderCommand(string OrderId, string CustomerId);

/// <summary>Summary returned by the order message-expiration example.</summary>
public sealed record OrderExpirationSummary(bool Expired, DateTimeOffset? ExpiresAt, string? Reason);

/// <summary>
/// Service that stamps order commands with a processing deadline and rejects stale commands.
/// </summary>
public sealed class OrderMessageExpirationService(MessageExpiration<ExpiringOrderCommand> expiration)
{
    public Message<ExpiringOrderCommand> Accept(ExpiringOrderCommand command)
        => expiration.Stamp(Message<ExpiringOrderCommand>.Create(command));

    public OrderExpirationSummary Evaluate(Message<ExpiringOrderCommand> message)
    {
        var result = expiration.Evaluate(message);
        return new OrderExpirationSummary(result.Expired, result.ExpiresAt, result.Reason);
    }
}

/// <summary>Fluent message-expiration builder used by applications that do not enable generators.</summary>
public static class OrderMessageExpirations
{
    public static MessageExpiration<ExpiringOrderCommand> Create(DateTimeOffset? now = null)
        => MessageExpiration<ExpiringOrderCommand>.Create()
            .Name("order-message-expiration")
            .Header("x-order-expires-at")
            .DefaultTtl(TimeSpan.FromMinutes(20))
            .Clock(() => now ?? DateTimeOffset.UtcNow)
            .ExpiredReason("Order command expired before fulfillment accepted it.")
            .Build();
}

/// <summary>Source-generated message-expiration policy for order commands.</summary>
[GenerateMessageExpiration(
    typeof(ExpiringOrderCommand),
    FactoryName = "Create",
    PolicyName = "order-message-expiration",
    HeaderName = "x-order-expires-at",
    DefaultTtlMilliseconds = 1200000,
    ExpiredReason = "Order command expired before fulfillment accepted it.")]
public static partial class GeneratedOrderMessageExpiration;

/// <summary>Runner that demonstrates both fluent and generated message-expiration paths.</summary>
public sealed class OrderMessageExpirationExampleRunner(OrderMessageExpirationService service)
{
    public OrderExpirationSummary RunGenerated(ExpiringOrderCommand command)
    {
        var accepted = service.Accept(command);
        return service.Evaluate(accepted);
    }

    public static OrderExpirationSummary RunFluent(ExpiringOrderCommand command, DateTimeOffset now)
    {
        var expiration = OrderMessageExpirations.Create(now);
        var accepted = expiration.Stamp(Message<ExpiringOrderCommand>.Create(command));
        var result = expiration.Evaluate(accepted);
        return new OrderExpirationSummary(result.Expired, result.ExpiresAt, result.Reason);
    }
}

/// <summary>DI helpers for importing the order message-expiration example into standard .NET containers.</summary>
public static class OrderMessageExpirationExampleServiceCollectionExtensions
{
    public static IServiceCollection AddOrderMessageExpirationDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedOrderMessageExpiration.Create());
        services.AddSingleton<OrderMessageExpirationService>();
        services.AddSingleton<OrderMessageExpirationExampleRunner>();
        return services;
    }
}
