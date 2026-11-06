using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.Sidecar;
using PatternKit.Generators.Sidecar;

namespace PatternKit.Examples.SidecarDemo;

public sealed record OrderTelemetryRequest(string OrderId, decimal Total);

public sealed record OrderTelemetryResponse(string Confirmation, string TraceId);

public interface IOrderTelemetrySink
{
    void Capture(string eventName, string value);
}

public sealed class DemoOrderTelemetrySink : IOrderTelemetrySink
{
    public List<string> Captured { get; } = [];

    public void Capture(string eventName, string value) => Captured.Add($"{eventName}:{value}");
}

public sealed class OrderTelemetrySidecarService(Sidecar<OrderTelemetryRequest, OrderTelemetryResponse> sidecar)
{
    public SidecarResult<OrderTelemetryResponse> Submit(OrderTelemetryRequest request) => sidecar.Invoke(request);
}

public static class OrderTelemetrySidecars
{
    public static Sidecar<OrderTelemetryRequest, OrderTelemetryResponse> CreateFluent(IOrderTelemetrySink telemetry)
        => Sidecar<OrderTelemetryRequest, OrderTelemetryResponse>.Create("order-telemetry-sidecar")
            .Before("trace-context", AddTraceContext)
            .After("telemetry", (ctx, response) => telemetry.Capture("order.accepted", response.Confirmation))
            .Handle(SubmitOrder)
            .Build();

    public static void AddTraceContext(SidecarContext<OrderTelemetryRequest> ctx)
        => ctx.Items["trace-id"] = $"trace-{ctx.Request.OrderId}";

    public static OrderTelemetryResponse SubmitOrder(SidecarContext<OrderTelemetryRequest> ctx)
        => new($"ACCEPTED-{ctx.Request.OrderId}", (string)ctx.Items["trace-id"]);
}

[GenerateSidecar(typeof(OrderTelemetryRequest), typeof(OrderTelemetryResponse), FactoryMethodName = "Create", SidecarName = "order-telemetry-sidecar")]
public static partial class GeneratedOrderTelemetrySidecar
{
    [SidecarBefore("trace-context")]
    private static void AddTraceContext(SidecarContext<OrderTelemetryRequest> ctx)
        => OrderTelemetrySidecars.AddTraceContext(ctx);

    [SidecarAfter("telemetry")]
    private static void CaptureTelemetry(SidecarContext<OrderTelemetryRequest> ctx, OrderTelemetryResponse response)
        => ctx.Items["telemetry"] = response.Confirmation;

    [SidecarHandler]
    private static OrderTelemetryResponse SubmitOrder(SidecarContext<OrderTelemetryRequest> ctx)
        => OrderTelemetrySidecars.SubmitOrder(ctx);
}

public sealed class OrderTelemetrySidecarDemoRunner(OrderTelemetrySidecarService service)
{
    public SidecarResult<OrderTelemetryResponse> RunGenerated(OrderTelemetryRequest request) => service.Submit(request);

    public static SidecarResult<OrderTelemetryResponse> RunFluent(OrderTelemetryRequest request)
        => OrderTelemetrySidecars.CreateFluent(new DemoOrderTelemetrySink()).Invoke(request);
}

public static class OrderTelemetrySidecarServiceCollectionExtensions
{
    public static IServiceCollection AddOrderTelemetrySidecarDemo(this IServiceCollection services)
    {
        services.AddSingleton<IOrderTelemetrySink, DemoOrderTelemetrySink>();
        services.AddSingleton(static _ => GeneratedOrderTelemetrySidecar.Create());
        services.AddSingleton<OrderTelemetrySidecarService>();
        services.AddSingleton<OrderTelemetrySidecarDemoRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrderTelemetrySidecar(this IEndpointRouteBuilder endpoints, string pattern = "/orders/{orderId}/sidecar")
    {
        endpoints.MapPost(pattern, (string orderId, OrderTelemetryRequestBody body, OrderTelemetrySidecarService service) =>
            Results.Ok(service.Submit(new OrderTelemetryRequest(orderId, body.Total))))
            .WithName("OrderTelemetrySidecar");
        return endpoints;
    }
}

public sealed record OrderTelemetryRequestBody(decimal Total);
