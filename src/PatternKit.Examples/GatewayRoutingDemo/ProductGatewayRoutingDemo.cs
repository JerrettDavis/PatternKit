using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.GatewayRouting;
using PatternKit.Generators.GatewayRouting;

namespace PatternKit.Examples.GatewayRoutingDemo;

public sealed record ProductGatewayRequest(string Path, string TenantId);

public sealed record ProductGatewayResponse(string Source, string Body);

public interface IProductInventoryApi { ProductGatewayResponse Get(ProductGatewayRequest request); }

public interface IProductPricingApi { ProductGatewayResponse Get(ProductGatewayRequest request); }

public sealed class DemoProductInventoryApi : IProductInventoryApi
{
    public ProductGatewayResponse Get(ProductGatewayRequest request) => new("inventory", $"stock:{request.TenantId}:{request.Path}");
}

public sealed class DemoProductPricingApi : IProductPricingApi
{
    public ProductGatewayResponse Get(ProductGatewayRequest request) => new("pricing", $"price:{request.TenantId}:{request.Path}");
}

public sealed class ProductGatewayRoutingService(GatewayRouting<ProductGatewayRequest, ProductGatewayResponse> router)
{
    public GatewayRoutingResult<ProductGatewayResponse> Route(ProductGatewayRequest request) => router.Route(request);
}

public static class ProductGatewayRoutes
{
    public static GatewayRouting<ProductGatewayRequest, ProductGatewayResponse> CreateFluent(IProductInventoryApi inventory, IProductPricingApi pricing)
        => GatewayRouting<ProductGatewayRequest, ProductGatewayResponse>.Create("product-gateway-routing")
            .Route("inventory", IsInventory, inventory.Get)
            .Route("pricing", IsPricing, pricing.Get)
            .Fallback("not-found", static request => new("fallback", $"not-found:{request.Path}"))
            .Build();

    public static bool IsInventory(ProductGatewayRequest request) => request.Path.StartsWith("/inventory/", StringComparison.OrdinalIgnoreCase);

    public static bool IsPricing(ProductGatewayRequest request) => request.Path.StartsWith("/pricing/", StringComparison.OrdinalIgnoreCase);
}

[GenerateGatewayRouting(typeof(ProductGatewayRequest), typeof(ProductGatewayResponse), FactoryMethodName = "Create", GatewayName = "product-gateway-routing")]
public static partial class GeneratedProductGatewayRouting
{
    [GatewayRoute("inventory")]
    private static bool IsInventory(ProductGatewayRequest request) => ProductGatewayRoutes.IsInventory(request);

    [GatewayRouteHandler("inventory")]
    private static ProductGatewayResponse Inventory(ProductGatewayRequest request) => new("inventory", $"stock:{request.TenantId}:{request.Path}");

    [GatewayRoute("pricing")]
    private static bool IsPricing(ProductGatewayRequest request) => ProductGatewayRoutes.IsPricing(request);

    [GatewayRouteHandler("pricing")]
    private static ProductGatewayResponse Pricing(ProductGatewayRequest request) => new("pricing", $"price:{request.TenantId}:{request.Path}");

    [GatewayRouteFallback("not-found")]
    private static ProductGatewayResponse NotFound(ProductGatewayRequest request) => new("fallback", $"not-found:{request.Path}");
}

public sealed class ProductGatewayRoutingDemoRunner(ProductGatewayRoutingService service)
{
    public GatewayRoutingResult<ProductGatewayResponse> RunGenerated(ProductGatewayRequest request) => service.Route(request);

    public static GatewayRoutingResult<ProductGatewayResponse> RunFluent(ProductGatewayRequest request)
        => ProductGatewayRoutes.CreateFluent(new DemoProductInventoryApi(), new DemoProductPricingApi()).Route(request);
}

public static class ProductGatewayRoutingServiceCollectionExtensions
{
    public static IServiceCollection AddProductGatewayRoutingDemo(this IServiceCollection services)
    {
        services.AddSingleton<IProductInventoryApi, DemoProductInventoryApi>();
        services.AddSingleton<IProductPricingApi, DemoProductPricingApi>();
        services.AddSingleton(static _ => GeneratedProductGatewayRouting.Create());
        services.AddSingleton<ProductGatewayRoutingService>();
        services.AddSingleton<ProductGatewayRoutingDemoRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapProductGatewayRouting(this IEndpointRouteBuilder endpoints, string pattern = "/product-gateway/{tenantId}/{**path}")
    {
        endpoints.MapGet(pattern, (string tenantId, string path, ProductGatewayRoutingService service) =>
            Results.Ok(service.Route(new ProductGatewayRequest("/" + path, tenantId))))
            .WithName("ProductGatewayRouting");
        return endpoints;
    }
}
