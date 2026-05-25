using BenchmarkDotNet.Attributes;
using PatternKit.Generators.Proxy;
using PatternKit.Structural.Proxy;

namespace PatternKit.Benchmarks.Structural;

[BenchmarkCategory("Structural", "GoF", "Proxy")]
public class ProxyBenchmarks
{
    private static readonly PriceRequest Request = new("SKU-100", 3);

    [Benchmark(Baseline = true, Description = "Fluent: create proxy")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Proxy<PriceRequest, decimal> Fluent_CreateProxy()
        => Proxy<PriceRequest, decimal>
            .Create(static request => request.Quantity * 12.50m)
            .Intercept(static (PriceRequest request, Proxy<PriceRequest, decimal>.Subject next) =>
            {
                var normalized = request with { Sku = request.Sku.ToUpperInvariant() };
                var total = next(normalized);
                return normalized.Sku.StartsWith("SKU-", StringComparison.Ordinal) ? total : 0m;
            })
            .Build();

    [Benchmark(Description = "Generated: create proxy")]
    [BenchmarkCategory("Generated", "Construction")]
    public IPriceService Generated_CreateProxy()
        => new PriceServiceProxy(new PriceService());

    [Benchmark(Description = "Fluent: calculate price")]
    [BenchmarkCategory("Fluent", "Execution")]
    public decimal Fluent_CalculatePrice()
        => Fluent_CreateProxy().Execute(Request);

    [Benchmark(Description = "Generated: calculate price")]
    [BenchmarkCategory("Generated", "Execution")]
    public decimal Generated_CalculatePrice()
        => new PriceServiceProxy(new PriceService()).Calculate(Request);
}

public sealed record PriceRequest(string Sku, int Quantity);

[GenerateProxy(InterceptorMode = ProxyInterceptorMode.None)]
public partial interface IPriceService
{
    decimal Calculate(PriceRequest request);
}

public sealed class PriceService : IPriceService
{
    public decimal Calculate(PriceRequest request) => request.Quantity * 12.50m;
}
