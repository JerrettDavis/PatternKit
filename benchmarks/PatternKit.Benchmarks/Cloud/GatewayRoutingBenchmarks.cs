using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.GatewayRouting;
using PatternKit.Examples.GatewayRoutingDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "GatewayRouting")]
public class GatewayRoutingBenchmarks
{
    private static readonly ProductGatewayRequest Request = new("/inventory/SKU-42", "tenant-a");
    private static readonly DemoProductInventoryApi Inventory = new();
    private static readonly DemoProductPricingApi Pricing = new();
    private readonly GatewayRouting<ProductGatewayRequest, ProductGatewayResponse> _fluent =
        ProductGatewayRoutes.CreateFluent(Inventory, Pricing);
    private readonly GatewayRouting<ProductGatewayRequest, ProductGatewayResponse> _generated =
        GeneratedProductGatewayRouting.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create gateway router")]
    [BenchmarkCategory("Fluent", "Construction")]
    public GatewayRouting<ProductGatewayRequest, ProductGatewayResponse> Fluent_CreateRouter()
        => ProductGatewayRoutes.CreateFluent(Inventory, Pricing);

    [Benchmark(Description = "Generated: create gateway router")]
    [BenchmarkCategory("Generated", "Construction")]
    public GatewayRouting<ProductGatewayRequest, ProductGatewayResponse> Generated_CreateRouter()
        => GeneratedProductGatewayRouting.Create();

    [Benchmark(Description = "Fluent: route inventory request")]
    [BenchmarkCategory("Fluent", "Execution")]
    public GatewayRoutingResult<ProductGatewayResponse> Fluent_RouteInventoryRequest()
        => _fluent.Route(Request);

    [Benchmark(Description = "Generated: route inventory request")]
    [BenchmarkCategory("Generated", "Execution")]
    public GatewayRoutingResult<ProductGatewayResponse> Generated_RouteInventoryRequest()
        => _generated.Route(Request);
}
