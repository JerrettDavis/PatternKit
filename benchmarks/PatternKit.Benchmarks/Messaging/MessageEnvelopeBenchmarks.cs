using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageEnvelope")]
public class MessageEnvelopeBenchmarks
{
    private static readonly OrderAccepted Accepted = new("order-42", 199.95m);

    [Benchmark(Baseline = true, Description = "Fluent: create message envelope")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Message<OrderAccepted> Fluent_CreateMessageEnvelope()
        => Message<OrderAccepted>
            .Create(Accepted)
            .WithMessageId("msg-100")
            .WithCorrelationId("order-42")
            .WithCausationId("checkout-7")
            .WithIdempotencyKey("order-42:accepted")
            .WithContentType("application/vnd.patternkit.order+json");

    [Benchmark(Description = "Generated: create message envelope")]
    [BenchmarkCategory("Generated", "Construction")]
    public Message<OrderAccepted> Generated_CreateMessageEnvelope()
        => GeneratedOrderAcceptedEnvelope.CreateAccepted(
            Accepted,
            "msg-100",
            "order-42",
            "checkout-7",
            "order-42:accepted",
            "application/vnd.patternkit.order+json");

    [Benchmark(Description = "Fluent: enrich message context")]
    [BenchmarkCategory("Fluent", "Execution")]
    public Summary Fluent_EnrichMessageContext()
        => MessageEnvelopeExample.RunFluent();

    [Benchmark(Description = "Generated: enrich message context")]
    [BenchmarkCategory("Generated", "Execution")]
    public Summary Generated_EnrichMessageContext()
        => MessageEnvelopeExample.RunGenerated();
}
