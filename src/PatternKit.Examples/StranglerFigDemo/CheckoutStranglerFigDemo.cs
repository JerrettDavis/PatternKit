using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.StranglerFig;
using PatternKit.Generators.StranglerFig;

namespace PatternKit.Examples.StranglerFigDemo;

public sealed record CheckoutMigrationRequest(string TenantId, string OrderId, decimal Total);

public sealed record CheckoutMigrationResponse(string Confirmation, string Processor, decimal CapturedTotal);

public interface ILegacyCheckoutSystem
{
    CheckoutMigrationResponse Submit(CheckoutMigrationRequest request);
}

public interface IModernCheckoutSystem
{
    CheckoutMigrationResponse Submit(CheckoutMigrationRequest request);
}

public sealed class DemoLegacyCheckoutSystem : ILegacyCheckoutSystem
{
    public CheckoutMigrationResponse Submit(CheckoutMigrationRequest request)
        => new($"LEGACY-{request.OrderId}", "legacy-mainframe", request.Total);
}

public sealed class DemoModernCheckoutSystem : IModernCheckoutSystem
{
    public CheckoutMigrationResponse Submit(CheckoutMigrationRequest request)
        => new($"MODERN-{request.OrderId}", "modern-checkout", request.Total);
}

public sealed class CheckoutMigrationService(StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse> migration)
{
    public StranglerFigResult<CheckoutMigrationResponse> Submit(CheckoutMigrationRequest request)
        => migration.Route(request);
}

public static class CheckoutMigrationRoutes
{
    public static StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse> CreateFluent(
        ILegacyCheckoutSystem legacy,
        IModernCheckoutSystem modern)
        => StranglerFig<CheckoutMigrationRequest, CheckoutMigrationResponse>.Create("checkout-strangler")
            .RouteToModern("enterprise-tenant", IsEnterpriseTenant)
            .RouteToModern("large-order-pilot", IsLargeOrderPilot)
            .Legacy(legacy.Submit)
            .Modern(modern.Submit)
            .Build();

    public static bool IsEnterpriseTenant(CheckoutMigrationRequest request)
        => request.TenantId.StartsWith("enterprise-", StringComparison.OrdinalIgnoreCase);

    public static bool IsLargeOrderPilot(CheckoutMigrationRequest request)
        => request.Total >= 1_000m;
}

[GenerateStranglerFig(typeof(CheckoutMigrationRequest), typeof(CheckoutMigrationResponse), FactoryMethodName = "Create", MigrationName = "checkout-strangler")]
public static partial class GeneratedCheckoutMigration
{
    [StranglerFigRoute("enterprise-tenant")]
    private static bool IsEnterpriseTenant(CheckoutMigrationRequest request)
        => CheckoutMigrationRoutes.IsEnterpriseTenant(request);

    [StranglerFigRoute("large-order-pilot")]
    private static bool IsLargeOrderPilot(CheckoutMigrationRequest request)
        => CheckoutMigrationRoutes.IsLargeOrderPilot(request);

    [StranglerFigLegacy]
    private static CheckoutMigrationResponse Legacy(CheckoutMigrationRequest request)
        => new($"LEGACY-{request.OrderId}", "legacy-mainframe", request.Total);

    [StranglerFigModern]
    private static CheckoutMigrationResponse Modern(CheckoutMigrationRequest request)
        => new($"MODERN-{request.OrderId}", "modern-checkout", request.Total);
}

public sealed class CheckoutStranglerFigDemoRunner(CheckoutMigrationService service)
{
    public StranglerFigResult<CheckoutMigrationResponse> RunGenerated(CheckoutMigrationRequest request)
        => service.Submit(request);

    public static StranglerFigResult<CheckoutMigrationResponse> RunFluent(CheckoutMigrationRequest request)
    {
        var migration = CheckoutMigrationRoutes.CreateFluent(new DemoLegacyCheckoutSystem(), new DemoModernCheckoutSystem());
        return migration.Route(request);
    }
}

public static class CheckoutStranglerFigServiceCollectionExtensions
{
    public static IServiceCollection AddCheckoutStranglerFigDemo(this IServiceCollection services)
    {
        services.AddSingleton<ILegacyCheckoutSystem, DemoLegacyCheckoutSystem>();
        services.AddSingleton<IModernCheckoutSystem, DemoModernCheckoutSystem>();
        services.AddSingleton(static _ => GeneratedCheckoutMigration.Create());
        services.AddSingleton<CheckoutMigrationService>();
        services.AddSingleton<CheckoutStranglerFigDemoRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapCheckoutStranglerFig(this IEndpointRouteBuilder endpoints, string pattern = "/checkout/{tenantId}/{orderId}")
    {
        endpoints.MapPost(pattern, (string tenantId, string orderId, CheckoutRequestBody body, CheckoutMigrationService service) =>
            Results.Ok(service.Submit(new CheckoutMigrationRequest(tenantId, orderId, body.Total))))
            .WithName("CheckoutStranglerFig");
        return endpoints;
    }
}

public sealed record CheckoutRequestBody(decimal Total);
