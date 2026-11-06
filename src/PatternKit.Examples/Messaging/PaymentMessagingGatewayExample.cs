using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Gateways;

namespace PatternKit.Examples.Messaging;

public sealed record PaymentAuthorizationRequest(string OrderId, decimal Amount);

public sealed record PaymentAuthorizationDecision(string AuthorizationCode, bool Approved);

public sealed record PaymentGatewaySummary(bool Completed, bool Approved, string? AuthorizationCode, int RequestCount);

public sealed class PaymentMessagingGatewayService(MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision> gateway, MessageChannel<PaymentAuthorizationRequest> requests)
{
    public PaymentGatewaySummary Authorize(PaymentAuthorizationRequest request)
    {
        var result = gateway.Invoke(request);
        return new(
            result.Completed,
            result.Response?.Payload.Approved ?? false,
            result.Response?.Payload.AuthorizationCode,
            requests.Count);
    }
}

public static class PaymentMessagingGateways
{
    public static MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision> Create(MessageChannel<PaymentAuthorizationRequest> requests)
        => MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision>.Create("payment-authorization-gateway")
            .SendTo(requests)
            .Handle(Authorize)
            .Build();

    public static Message<PaymentAuthorizationDecision> Authorize(Message<PaymentAuthorizationRequest> request, MessageContext context)
    {
        var approved = request.Payload.Amount <= 500m;
        var code = approved ? $"AUTH-{request.Payload.OrderId}" : "DECLINED";
        return Message<PaymentAuthorizationDecision>.Create(new(code, approved));
    }
}

[GenerateMessagingGateway(typeof(PaymentAuthorizationRequest), typeof(PaymentAuthorizationDecision), FactoryName = "Create", GatewayName = "payment-authorization-gateway")]
public static partial class GeneratedPaymentMessagingGateway
{
    [MessagingGatewayHandler]
    private static Message<PaymentAuthorizationDecision> Authorize(Message<PaymentAuthorizationRequest> request, MessageContext context)
        => PaymentMessagingGateways.Authorize(request, context);
}

public sealed class PaymentMessagingGatewayExampleRunner(PaymentMessagingGatewayService service)
{
    public PaymentGatewaySummary RunGenerated(PaymentAuthorizationRequest request) => service.Authorize(request);

    public static PaymentGatewaySummary RunFluent(PaymentAuthorizationRequest request)
    {
        var channel = MessageChannel<PaymentAuthorizationRequest>.Create("payment-requests").Build();
        return new PaymentMessagingGatewayService(PaymentMessagingGateways.Create(channel), channel).Authorize(request);
    }

    public static PaymentGatewaySummary RunGeneratedStatic(PaymentAuthorizationRequest request)
    {
        var channel = MessageChannel<PaymentAuthorizationRequest>.Create("payment-requests").Build();
        return new PaymentMessagingGatewayService(GeneratedPaymentMessagingGateway.Create(channel), channel).Authorize(request);
    }
}

public static class PaymentMessagingGatewayExampleServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentMessagingGatewayDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => MessageChannel<PaymentAuthorizationRequest>.Create("payment-requests").Build());
        services.AddSingleton(sp => GeneratedPaymentMessagingGateway.Create(sp.GetRequiredService<MessageChannel<PaymentAuthorizationRequest>>()));
        services.AddSingleton<PaymentMessagingGatewayService>();
        services.AddSingleton<PaymentMessagingGatewayExampleRunner>();
        return services;
    }
}
