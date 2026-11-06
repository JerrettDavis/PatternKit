using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.Ambassador;
using PatternKit.Examples.AmbassadorDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "Ambassador")]
public class AmbassadorBenchmarks
{
    private static readonly InventoryAmbassadorRequest Request = new("sku-42", "tenant-a");
    private static readonly DemoInventoryAvailabilityClient Client = new();
    private readonly Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse> _fluent =
        InventoryAmbassadors.CreateFluent(Client);
    private readonly Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse> _generated =
        GeneratedInventoryAmbassador.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create ambassador")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse> Fluent_CreateAmbassador()
        => InventoryAmbassadors.CreateFluent(Client);

    [Benchmark(Description = "Generated: create ambassador")]
    [BenchmarkCategory("Generated", "Construction")]
    public Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse> Generated_CreateAmbassador()
        => GeneratedInventoryAmbassador.Create();

    [Benchmark(Description = "Fluent: transform, trace, call")]
    [BenchmarkCategory("Fluent", "Execution")]
    public AmbassadorResult<InventoryAmbassadorResponse> Fluent_Invoke()
        => _fluent.Invoke(Request);

    [Benchmark(Description = "Generated: transform, trace, call")]
    [BenchmarkCategory("Generated", "Execution")]
    public AmbassadorResult<InventoryAmbassadorResponse> Generated_Invoke()
        => _generated.Invoke(Request);
}
