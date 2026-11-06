using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.StranglerFig;
using PatternKit.Examples.StranglerFigDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "StranglerFig")]
public class StranglerFigBenchmarks
{
    private static readonly CheckoutMigrationRequest Request = new("enterprise-a", "O-200", 50m);
    private static readonly DemoLegacyCheckoutSystem Legacy = new();
    private static readonly DemoModernCheckoutSystem Modern = new();
    private readonly StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse> _fluent =
        CheckoutMigrationRoutes.CreateFluent(Legacy, Modern);
    private readonly StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse> _generated =
        GeneratedCheckoutMigration.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create Strangler Fig migration")]
    [BenchmarkCategory("Fluent", "Construction")]
    public StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse> Fluent_CreateMigration()
        => CheckoutMigrationRoutes.CreateFluent(Legacy, Modern);

    [Benchmark(Description = "Generated: create Strangler Fig migration")]
    [BenchmarkCategory("Generated", "Construction")]
    public StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse> Generated_CreateMigration()
        => GeneratedCheckoutMigration.Create();

    [Benchmark(Description = "Fluent: route enterprise checkout")]
    [BenchmarkCategory("Fluent", "Execution")]
    public StranglerFigResult<CheckoutMigrationResponse> Fluent_RouteEnterpriseCheckout()
        => _fluent.Route(Request);

    [Benchmark(Description = "Generated: route enterprise checkout")]
    [BenchmarkCategory("Generated", "Execution")]
    public StranglerFigResult<CheckoutMigrationResponse> Generated_RouteEnterpriseCheckout()
        => _generated.Route(Request);
}
