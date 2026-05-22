using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.Ambassador;
using PatternKit.Generators.Ambassador;

namespace PatternKit.Examples.AmbassadorDemo;

public sealed record InventoryAmbassadorRequest(string Sku, string Tenant);

public sealed record InventoryAmbassadorResponse(string Sku, string Status, string Source);

public interface IInventoryAvailabilityClient
{
    InventoryAmbassadorResponse GetAvailability(InventoryAmbassadorRequest request);
}

public sealed class DemoInventoryAvailabilityClient : IInventoryAvailabilityClient
{
    public InventoryAmbassadorResponse GetAvailability(InventoryAmbassadorRequest request)
        => new(request.Sku, "available", "inventory-api");
}

public sealed class InventoryAmbassadorService(Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse> ambassador)
{
    public InventoryAmbassadorResponse GetAvailability(string tenant, string sku)
    {
        var result = ambassador.Invoke(new InventoryAmbassadorRequest(sku, tenant));
        if (result.Failed)
            throw new InvalidOperationException("Inventory ambassador could not complete the outbound call.", result.Exception);

        return result.Response!;
    }
}

public static class InventoryAmbassadors
{
    public static Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse> CreateFluent(IInventoryAvailabilityClient client)
        => Ambassador<InventoryAmbassadorRequest, InventoryAmbassadorResponse>.Create("inventory-ambassador")
            .Transform(static request => request with { Sku = request.Sku.ToUpperInvariant() })
            .ConnectionPolicy(static request => !request.Tenant.Equals("blocked", StringComparison.OrdinalIgnoreCase))
            .Telemetry("trace", static ctx => ctx.Items["tenant"] = ctx.Request.Tenant)
            .Call(ctx => client.GetAvailability(ctx.Request))
            .Fallback(static ctx => new(ctx.Request.Sku, "cached", "fallback-cache"))
            .Build();
}

[GenerateAmbassador(typeof(InventoryAmbassadorRequest), typeof(InventoryAmbassadorResponse), FactoryMethodName = "Create", AmbassadorName = "inventory-ambassador")]
public static partial class GeneratedInventoryAmbassador
{
    [AmbassadorTransform]
    private static InventoryAmbassadorRequest Normalize(InventoryAmbassadorRequest request)
        => request with { Sku = request.Sku.ToUpperInvariant() };

    [AmbassadorConnectionPolicy]
    private static bool CanConnect(InventoryAmbassadorRequest request)
        => !request.Tenant.Equals("blocked", StringComparison.OrdinalIgnoreCase);

    [AmbassadorTelemetry("trace")]
    private static void Trace(AmbassadorContext<InventoryAmbassadorRequest> ctx)
        => ctx.Items["tenant"] = ctx.Request.Tenant;

    [AmbassadorCall]
    private static InventoryAmbassadorResponse Call(AmbassadorContext<InventoryAmbassadorRequest> ctx)
        => new(ctx.Request.Sku, "available", "inventory-api");

    [AmbassadorFallback]
    private static InventoryAmbassadorResponse Fallback(AmbassadorContext<InventoryAmbassadorRequest> ctx)
        => new(ctx.Request.Sku, "cached", "fallback-cache");
}

public sealed class InventoryAmbassadorDemoRunner(InventoryAmbassadorService service)
{
    public InventoryAmbassadorResponse RunGenerated(string tenant, string sku) => service.GetAvailability(tenant, sku);

    public static InventoryAmbassadorResponse RunFluent()
    {
        var ambassador = InventoryAmbassadors.CreateFluent(new DemoInventoryAvailabilityClient());
        var result = ambassador.Invoke(new InventoryAmbassadorRequest("sku-1", "tenant-a"));
        return result.Response!;
    }
}

public static class InventoryAmbassadorServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryAmbassadorDemo(this IServiceCollection services)
    {
        services.AddSingleton<IInventoryAvailabilityClient, DemoInventoryAvailabilityClient>();
        services.AddSingleton(static _ => GeneratedInventoryAmbassador.Create());
        services.AddSingleton<InventoryAmbassadorService>();
        services.AddSingleton<InventoryAmbassadorDemoRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapInventoryAmbassador(this IEndpointRouteBuilder endpoints, string pattern = "/inventory/{tenant}/{sku}/availability")
    {
        endpoints.MapGet(pattern, (string tenant, string sku, InventoryAmbassadorService service) => Results.Ok(service.GetAvailability(tenant, sku)))
            .WithName("InventoryAmbassador");
        return endpoints;
    }
}
