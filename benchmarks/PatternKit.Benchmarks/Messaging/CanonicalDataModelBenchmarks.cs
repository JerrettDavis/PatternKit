using BenchmarkDotNet.Attributes;
using PatternKit.EnterpriseIntegration.CanonicalDataModel;
using PatternKit.Examples.CanonicalDataModelDemo;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "CanonicalDataModel")]
public class CanonicalDataModelBenchmarks
{
    private static readonly MarketplaceOrderDocument MarketplaceOrder = new("M-200", 7500, "usd");
    private static readonly PartnerOrderDocument PartnerOrder = new("P-100", 42.50m, "usd");

    [Benchmark(Baseline = true, Description = "Fluent: create canonical data model")]
    [BenchmarkCategory("Fluent", "Construction")]
    public CanonicalDataModel<CanonicalCommerceOrder> Fluent_CreateModel()
        => CanonicalOrderModels.CreateFluent();

    [Benchmark(Description = "Generated: create canonical data model")]
    [BenchmarkCategory("Generated", "Construction")]
    public CanonicalDataModel<CanonicalCommerceOrder> Generated_CreateModel()
        => GeneratedPartnerOrderCanonicalDataModel.Create();

    [Benchmark(Description = "Fluent: normalize marketplace order")]
    [BenchmarkCategory("Fluent", "Execution")]
    public CanonicalOrderSummary Fluent_NormalizeMarketplaceOrder()
    {
        var result = CanonicalOrderModels.CreateFluent().Normalize(MarketplaceOrder);
        var canonical = result.Value!;
        return new(result.ModelName, result.AdapterName, canonical.OrderId, canonical.Total, canonical.Currency);
    }

    [Benchmark(Description = "Generated: normalize partner order")]
    [BenchmarkCategory("Generated", "Execution")]
    public CanonicalOrderSummary Generated_NormalizePartnerOrder()
    {
        var result = GeneratedPartnerOrderCanonicalDataModel.Create().Normalize(PartnerOrder);
        var canonical = result.Value!;
        return new(result.ModelName, result.AdapterName, canonical.OrderId, canonical.Total, canonical.Currency);
    }
}
