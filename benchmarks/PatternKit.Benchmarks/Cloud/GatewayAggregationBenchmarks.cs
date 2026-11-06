using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.GatewayAggregation;
using PatternKit.Examples.GatewayAggregationDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "GatewayAggregation")]
public class GatewayAggregationBenchmarks
{
    private static readonly CustomerDashboardRequest Request = new("C-100");
    private static readonly DemoCustomerProfileClient Profiles = new();
    private static readonly DemoCustomerOrdersClient Orders = new();
    private static readonly DemoCustomerRecommendationClient Recommendations = new();
    private readonly GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse> _fluent =
        CustomerDashboardGateways.CreateFluent(Profiles, Orders, Recommendations);
    private readonly GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse> _generated =
        GeneratedCustomerDashboardGateway.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create aggregation gateway")]
    [BenchmarkCategory("Fluent", "Construction")]
    public GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse> Fluent_CreateGateway()
        => CustomerDashboardGateways.CreateFluent(Profiles, Orders, Recommendations);

    [Benchmark(Description = "Generated: create aggregation gateway")]
    [BenchmarkCategory("Generated", "Construction")]
    public GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse> Generated_CreateGateway()
        => GeneratedCustomerDashboardGateway.Create();

    [Benchmark(Description = "Fluent: aggregate dashboard")]
    [BenchmarkCategory("Fluent", "Execution")]
    public CustomerDashboardResponse Fluent_AggregateDashboard()
        => _fluent.Aggregate(Request).Response!;

    [Benchmark(Description = "Generated: aggregate dashboard")]
    [BenchmarkCategory("Generated", "Execution")]
    public CustomerDashboardResponse Generated_AggregateDashboard()
        => _generated.Aggregate(Request).Response!;
}
