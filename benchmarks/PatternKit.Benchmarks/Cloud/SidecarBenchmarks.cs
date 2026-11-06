using BenchmarkDotNet.Attributes;
using PatternKit.Cloud.Sidecar;
using PatternKit.Examples.SidecarDemo;

namespace PatternKit.Benchmarks.Cloud;

[BenchmarkCategory("Cloud", "Sidecar")]
public class SidecarBenchmarks
{
    private static readonly OrderTelemetryRequest Request = new("O-200", 50m);
    private static readonly IOrderTelemetrySink Telemetry = new NoopOrderTelemetrySink();
    private readonly Sidecar<OrderTelemetryRequest, OrderTelemetryResponse> _fluent =
        OrderTelemetrySidecars.CreateFluent(Telemetry);
    private readonly Sidecar<OrderTelemetryRequest, OrderTelemetryResponse> _generated =
        GeneratedOrderTelemetrySidecar.Create();

    [Benchmark(Baseline = true, Description = "Fluent: create sidecar")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Sidecar<OrderTelemetryRequest, OrderTelemetryResponse> Fluent_CreateSidecar()
        => OrderTelemetrySidecars.CreateFluent(Telemetry);

    [Benchmark(Description = "Generated: create sidecar")]
    [BenchmarkCategory("Generated", "Construction")]
    public Sidecar<OrderTelemetryRequest, OrderTelemetryResponse> Generated_CreateSidecar()
        => GeneratedOrderTelemetrySidecar.Create();

    [Benchmark(Description = "Fluent: submit order through sidecar")]
    [BenchmarkCategory("Fluent", "Execution")]
    public SidecarResult<OrderTelemetryResponse> Fluent_SubmitOrder()
        => _fluent.Invoke(Request);

    [Benchmark(Description = "Generated: submit order through sidecar")]
    [BenchmarkCategory("Generated", "Execution")]
    public SidecarResult<OrderTelemetryResponse> Generated_SubmitOrder()
        => _generated.Invoke(Request);

    private sealed class NoopOrderTelemetrySink : IOrderTelemetrySink
    {
        public void Capture(string eventName, string value)
        {
        }
    }
}
