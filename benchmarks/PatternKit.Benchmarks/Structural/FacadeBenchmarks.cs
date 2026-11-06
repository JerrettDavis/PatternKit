using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Generators.Facade;
using PatternKit.Structural.Facade;

namespace PatternKit.Benchmarks.Structural;

[BenchmarkCategory("Structural", "GoF", "Facade")]
public class FacadeBenchmarks
{
    private static readonly ShipmentQuoteRequest Request = new("national", 12m, "standard");

    [Benchmark(Baseline = true, Description = "Fluent: create facade")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Facade<ShipmentQuoteRequest, string> Fluent_CreateFacade()
        => Facade<ShipmentQuoteRequest, string>
            .Create()
            .Operation("quote", static (in ShipmentQuoteRequest request) =>
            {
                var baseRate = request.Destination == "local" ? 5.99m : 19.99m;
                var surcharge = request.Weight > 5m ? (request.Weight - 5m) * 0.50m : 0m;
                return $"${baseRate + surcharge:F2}";
            })
            .Default(static (in _) => "Invalid operation")
            .Build();

    [Benchmark(Description = "Generated: create facade")]
    [BenchmarkCategory("Generated", "Construction")]
    public ShippingFacade Generated_CreateFacade()
        => new(new DeliveryEstimator(), new RateCalculator(), new ShippingValidator());

    [Benchmark(Description = "Fluent: get shipping quote")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_GetShippingQuote()
        => Fluent_CreateFacade().Execute("quote", Request);

    [Benchmark(Description = "Generated: get shipping quote")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_GetShippingQuote()
        => Generated_CreateFacade().GetQuote(Request.Destination, Request.Weight, Request.Speed);
}

public sealed record ShipmentQuoteRequest(string Destination, decimal Weight, string Speed);
