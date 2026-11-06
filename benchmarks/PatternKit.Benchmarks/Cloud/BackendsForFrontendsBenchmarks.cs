using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.BackendsForFrontends;
using PatternKit.Examples.BackendsForFrontendsDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "BackendsForFrontends")]
public class BackendsForFrontendsBenchmarks
{
    private static readonly CommerceClientRequest Request = new("web", "C-100");
    private static readonly DemoCommerceSummaryBackend Backend = new();
    private readonly BackendsForFrontends<CommerceClientRequest, CommerceClientResponse> _fluent =
        CommerceBackendsForFrontends.CreateFluent(Backend);
    private readonly BackendsForFrontends<CommerceClientRequest, CommerceClientResponse> _generated =
        GeneratedCommerceBackendsForFrontends.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create BFF router")]
    [BenchmarkCategory("Fluent", "Construction")]
    public BackendsForFrontends<CommerceClientRequest, CommerceClientResponse> Fluent_CreateRouter()
        => CommerceBackendsForFrontends.CreateFluent(Backend);

    [Benchmark(Description = "Generated: create BFF router")]
    [BenchmarkCategory("Generated", "Construction")]
    public BackendsForFrontends<CommerceClientRequest, CommerceClientResponse> Generated_CreateRouter()
        => GeneratedCommerceBackendsForFrontends.Create();

    [Benchmark(Description = "Fluent: shape web summary")]
    [BenchmarkCategory("Fluent", "Execution")]
    public CommerceClientResponse Fluent_ShapeWebSummary()
        => _fluent.Dispatch(Request).Response!;

    [Benchmark(Description = "Generated: shape web summary")]
    [BenchmarkCategory("Generated", "Execution")]
    public CommerceClientResponse Generated_ShapeWebSummary()
        => _generated.Dispatch(Request).Response!;
}
