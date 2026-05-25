using BenchmarkDotNet.Attributes;
using PatternKit.Generators.Adapter;
using PatternKit.Structural.Adapter;

namespace PatternKit.Benchmarks.Structural;

[BenchmarkCategory("Structural", "GoF", "Adapter")]
public class AdapterBenchmarks
{
    private static readonly LegacyShipment LegacyShipment = new("WH-100", 3, 16, "Austin");

    [Benchmark(Baseline = true, Description = "Fluent: create adapter")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Adapter<LegacyShipment, ModernShipment> Fluent_CreateAdapter()
        => Adapter<LegacyShipment, ModernShipment>
            .Create(static () => new ModernShipment())
            .Map(static (in LegacyShipment source, ModernShipment target) =>
            {
                target.TrackingCode = source.LegacyId;
                target.TotalOunces = source.Pounds * 16 + source.Ounces;
                target.Destination = source.Route;
            })
            .Require(static (in LegacyShipment _, ModernShipment target) =>
                target.TotalOunces <= 0 ? "Shipment weight is required." : null)
            .Build();

    [Benchmark(Description = "Generated: create adapter")]
    [BenchmarkCategory("Generated", "Construction")]
    public IModernShipment Generated_CreateAdapter()
        => new GeneratedShipmentAdapter(LegacyShipment);

    [Benchmark(Description = "Fluent: adapt shipment")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_AdaptShipment()
    {
        var shipment = Fluent_CreateAdapter().Adapt(LegacyShipment);
        return shipment.FormatLabel();
    }

    [Benchmark(Description = "Generated: adapt shipment")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_AdaptShipment()
    {
        IModernShipment adapter = new GeneratedShipmentAdapter(LegacyShipment);
        return adapter.FormatLabel();
    }
}

public sealed record LegacyShipment(string LegacyId, int Pounds, int Ounces, string Route);

public sealed class ModernShipment
{
    public string TrackingCode { get; set; } = string.Empty;

    public int TotalOunces { get; set; }

    public string Destination { get; set; } = string.Empty;

    public string FormatLabel() => $"{TrackingCode}:{Destination}:{TotalOunces}";
}

public interface IModernShipment
{
    string TrackingCode { get; }

    int TotalOunces { get; }

    string Destination { get; }

    string FormatLabel();
}

[GenerateAdapter(
    Target = typeof(IModernShipment),
    Adaptee = typeof(LegacyShipment),
    AdapterTypeName = "GeneratedShipmentAdapter")]
public static partial class ShipmentAdapterMappings
{
    [AdapterMap(TargetMember = nameof(IModernShipment.TrackingCode))]
    public static string MapTrackingCode(LegacyShipment shipment) => shipment.LegacyId;

    [AdapterMap(TargetMember = nameof(IModernShipment.TotalOunces))]
    public static int MapTotalOunces(LegacyShipment shipment) => shipment.Pounds * 16 + shipment.Ounces;

    [AdapterMap(TargetMember = nameof(IModernShipment.Destination))]
    public static string MapDestination(LegacyShipment shipment) => shipment.Route;

    [AdapterMap(TargetMember = nameof(IModernShipment.FormatLabel))]
    public static string MapFormatLabel(LegacyShipment shipment)
        => $"{shipment.LegacyId}:{shipment.Route}:{shipment.Pounds * 16 + shipment.Ounces}";
}
