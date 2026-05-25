using BenchmarkDotNet.Attributes;
using PatternKit.Generators.Bridge;
using PatternKit.Structural.Bridge;

namespace PatternKit.Benchmarks.Structural;

[BenchmarkCategory("Structural", "GoF", "Bridge")]
public class BridgeBenchmarks
{
    private static readonly ShipmentNotice Notice = new("order-100", "ops@example.com", 3);
    private readonly EmailNoticeChannel _channel = new();
    private readonly GeneratedNoticeRenderer _renderer = new();

    [Benchmark(Baseline = true, Description = "Fluent: create bridge")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Bridge<ShipmentNotice, string, EmailNoticeChannel> Fluent_CreateBridge()
        => Bridge<ShipmentNotice, string, EmailNoticeChannel>
            .Create(() => _channel)
            .Require(static (in ShipmentNotice notice, EmailNoticeChannel _) =>
                string.IsNullOrWhiteSpace(notice.Recipient) ? "Recipient is required." : null)
            .Operation(static (in ShipmentNotice notice, EmailNoticeChannel channel) => channel.Render(notice))
            .After(static (in ShipmentNotice _, EmailNoticeChannel __, string label) => label.ToUpperInvariant())
            .Build();

    [Benchmark(Description = "Generated: create bridge")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedShipmentNoticeDefault Generated_CreateBridge()
        => new(_renderer);

    [Benchmark(Description = "Fluent: render notice")]
    [BenchmarkCategory("Fluent", "Execution")]
    public string Fluent_RenderNotice()
        => Fluent_CreateBridge().Execute(Notice);

    [Benchmark(Description = "Generated: render notice")]
    [BenchmarkCategory("Generated", "Execution")]
    public string Generated_RenderNotice()
        => new GeneratedShipmentNoticeDefault(_renderer).Render(Notice);
}

public sealed record ShipmentNotice(string OrderId, string Recipient, int PackageCount);

public sealed class EmailNoticeChannel
{
    public string Render(in ShipmentNotice notice)
        => $"{notice.Recipient}:{notice.OrderId}:{notice.PackageCount}";
}

[BridgeImplementor]
public partial interface IGeneratedNoticeRenderer
{
    string RenderNotice(ShipmentNotice notice);
}

public sealed class GeneratedNoticeRenderer : IGeneratedNoticeRenderer
{
    public string RenderNotice(ShipmentNotice notice)
        => $"{notice.Recipient}:{notice.OrderId}:{notice.PackageCount}".ToUpperInvariant();
}

[BridgeAbstraction(typeof(IGeneratedNoticeRenderer), GenerateDefault = true, DefaultTypeName = "GeneratedShipmentNoticeDefault")]
public partial class GeneratedShipmentNotice
{
    public string Render(ShipmentNotice notice) => RenderNotice(notice);
}
