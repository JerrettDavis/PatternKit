using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ContentEnricher")]
public class ContentEnricherBenchmarks
{
    private static readonly CustomerProfileUpdate Update = new("customer-100", " USER@EXAMPLE.COM ", null, false);

    [Benchmark(Baseline = true, Description = "Fluent: create content enricher")]
    [BenchmarkCategory("Fluent", "Construction")]
    public AsyncContentEnricher<CustomerProfileUpdate> Fluent_CreateContentEnricher()
        => CustomerProfileContentEnrichers.Create();

    [Benchmark(Description = "Generated: create content enricher")]
    [BenchmarkCategory("Generated", "Construction")]
    public AsyncContentEnricher<CustomerProfileUpdate> Generated_CreateContentEnricher()
        => GeneratedCustomerProfileContentEnricher.Create();

    [Benchmark(Description = "Fluent: enrich customer profile")]
    [BenchmarkCategory("Fluent", "Execution")]
    public CustomerProfileEnrichmentSummary Fluent_EnrichCustomerProfile()
        => CustomerProfileContentEnricherExampleRunner.RunFluentAsync(Update).AsTask().GetAwaiter().GetResult();

    [Benchmark(Description = "Generated: enrich customer profile")]
    [BenchmarkCategory("Generated", "Execution")]
    public CustomerProfileEnrichmentSummary Generated_EnrichCustomerProfile()
    {
        var result = GeneratedCustomerProfileContentEnricher.Create()
            .EnrichAsync(Message<CustomerProfileUpdate>.Create(Update))
            .AsTask()
            .GetAwaiter()
            .GetResult();
        var payload = result.Message.Payload;
        return new CustomerProfileEnrichmentSummary(
            payload.CustomerId,
            payload.Email ?? string.Empty,
            payload.Tier ?? string.Empty,
            payload.MarketingOptIn,
            result.StepResults.Count(step => step.Applied));
    }
}
