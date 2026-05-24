using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Gateways;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessagingGateway")]
public class MessagingGatewayBenchmarks
{
    private static readonly PaymentAuthorizationRequest Request = new("order-100", 149.95m);

    [Benchmark(Baseline = true, Description = "Fluent: create messaging gateway")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision> Fluent_CreateMessagingGateway()
        => PaymentMessagingGateways.Create(CreateChannel());

    [Benchmark(Description = "Generated: create messaging gateway")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision> Generated_CreateMessagingGateway()
        => GeneratedPaymentMessagingGateway.Create(CreateChannel());

    [Benchmark(Description = "Fluent: authorize payment request")]
    [BenchmarkCategory("Fluent", "Execution")]
    public PaymentGatewaySummary Fluent_AuthorizePaymentRequest()
        => PaymentMessagingGatewayExampleRunner.RunFluent(Request);

    [Benchmark(Description = "Generated: authorize payment request")]
    [BenchmarkCategory("Generated", "Execution")]
    public PaymentGatewaySummary Generated_AuthorizePaymentRequest()
        => PaymentMessagingGatewayExampleRunner.RunGeneratedStatic(Request);

    private static MessageChannel<PaymentAuthorizationRequest> CreateChannel()
        => MessageChannel<PaymentAuthorizationRequest>.Create("payment-requests").Build();
}
