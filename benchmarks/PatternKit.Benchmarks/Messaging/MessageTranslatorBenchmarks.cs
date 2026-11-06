using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Transformation;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageTranslator")]
public class MessageTranslatorBenchmarks
{
    private static readonly PartnerOrderAccepted PartnerOrder = new("partner-a", "EXT-100", 125m, "USD");
    private readonly MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted> _fluent =
        PartnerOrderTranslatorPolicies.CreateFluentTranslator();
    private readonly MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted> _generated =
        GeneratedPartnerOrderTranslator.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create translator")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted> Fluent_CreateTranslator()
        => PartnerOrderTranslatorPolicies.CreateFluentTranslator();

    [Benchmark(Description = "Generated: create translator")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted> Generated_CreateTranslator()
        => GeneratedPartnerOrderTranslator.Create();

    [Benchmark(Description = "Fluent: translate partner order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public PartnerOrderImportSummary Fluent_TranslatePartnerOrder()
        => PartnerOrderImportSummary.From("fluent", _fluent.Translate(CreateMessage()));

    [Benchmark(Description = "Generated: translate partner order")]
    [BenchmarkCategory("Generated", "Execution")]
    public PartnerOrderImportSummary Generated_TranslatePartnerOrder()
        => PartnerOrderImportSummary.From("source-generated", _generated.Translate(CreateMessage()));

    private static PatternKit.Messaging.Message<PartnerOrderAccepted> CreateMessage()
        => PatternKit.Messaging.Message<PartnerOrderAccepted>
            .Create(PartnerOrder)
            .WithMessageId("partner:EXT-100")
            .WithCorrelationId("EXT-100")
            .WithHeader("partner-id", "partner-a")
            .WithHeader("raw-signature", "demo-signature");
}
