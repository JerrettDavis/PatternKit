using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ScatterGather")]
public class ScatterGatherBenchmarks
{
    private static readonly SupplierQuoteRequest Request = new("SKU-100", 120, true);

    [Benchmark(Baseline = true, Description = "Fluent: create scatter-gather")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary> Fluent_CreateScatterGather()
        => SupplierQuoteScatterGathers.Create();

    [Benchmark(Description = "Generated: create scatter-gather")]
    [BenchmarkCategory("Generated", "Construction")]
    public ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary> Generated_CreateScatterGather()
        => GeneratedSupplierQuoteScatterGather.Create();

    [Benchmark(Description = "Fluent: request supplier quotes")]
    [BenchmarkCategory("Fluent", "Execution")]
    public SupplierQuoteSummary Fluent_RequestQuotes()
        => SupplierQuoteScatterGatherExampleRunner.RunFluent(Request);

    [Benchmark(Description = "Generated: request supplier quotes")]
    [BenchmarkCategory("Generated", "Execution")]
    public SupplierQuoteSummary Generated_RequestQuotes()
        => new SupplierQuoteService(GeneratedSupplierQuoteScatterGather.Create()).RequestQuotes(Request);
}
