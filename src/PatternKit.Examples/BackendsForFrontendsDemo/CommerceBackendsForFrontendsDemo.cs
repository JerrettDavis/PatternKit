using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.BackendsForFrontends;
using PatternKit.Generators.BackendsForFrontends;

namespace PatternKit.Examples.BackendsForFrontendsDemo;

public sealed record CommerceClientRequest(string Client, string CustomerId);

public sealed record CommerceClientResponse(string CustomerId, string Shape, int ItemCount, bool IncludesPromotions);

public interface ICommerceSummaryBackend
{
    int GetOpenItemCount(string customerId);
}

public sealed class DemoCommerceSummaryBackend : ICommerceSummaryBackend
{
    public int GetOpenItemCount(string customerId) => customerId.Length % 3 + 1;
}

public sealed class CommerceBackendsForFrontendsService(BackendsForFrontends<CommerceClientRequest, CommerceClientResponse> bff)
{
    public CommerceClientResponse GetSummary(string client, string customerId)
    {
        var result = bff.Dispatch(new CommerceClientRequest(client, customerId));
        if (result.Failed)
            throw new InvalidOperationException("Commerce client summary could not be shaped.", result.Exception);

        return result.Response!;
    }
}

public static class CommerceBackendsForFrontends
{
    public static BackendsForFrontends<CommerceClientRequest, CommerceClientResponse> CreateFluent(ICommerceSummaryBackend backend)
        => BackendsForFrontends<CommerceClientRequest, CommerceClientResponse>.Create("commerce-bff")
            .Frontend("mobile", static request => request.Client.Equals("mobile", StringComparison.OrdinalIgnoreCase), ctx => Mobile(ctx, backend))
            .Frontend("web", static request => request.Client.Equals("web", StringComparison.OrdinalIgnoreCase), ctx => Web(ctx, backend))
            .Fallback(ctx => Standard(ctx, backend))
            .Build();

    public static CommerceClientResponse Mobile(BackendsForFrontendsContext<CommerceClientRequest> ctx, ICommerceSummaryBackend backend)
        => new(ctx.Request.CustomerId, "compact", backend.GetOpenItemCount(ctx.Request.CustomerId), IncludesPromotions: false);

    public static CommerceClientResponse Web(BackendsForFrontendsContext<CommerceClientRequest> ctx, ICommerceSummaryBackend backend)
        => new(ctx.Request.CustomerId, "rich", backend.GetOpenItemCount(ctx.Request.CustomerId), IncludesPromotions: true);

    public static CommerceClientResponse Standard(BackendsForFrontendsContext<CommerceClientRequest> ctx, ICommerceSummaryBackend backend)
        => new(ctx.Request.CustomerId, "standard", backend.GetOpenItemCount(ctx.Request.CustomerId), IncludesPromotions: true);
}

[GenerateBackendsForFrontends(typeof(CommerceClientRequest), typeof(CommerceClientResponse), FactoryMethodName = "Create", GatewayName = "commerce-bff")]
public static partial class GeneratedCommerceBackendsForFrontends
{
    [FrontendSelector("mobile")]
    private static bool IsMobile(CommerceClientRequest request) => request.Client.Equals("mobile", StringComparison.OrdinalIgnoreCase);

    [FrontendHandler("mobile")]
    private static CommerceClientResponse Mobile(BackendsForFrontendsContext<CommerceClientRequest> ctx)
        => new(ctx.Request.CustomerId, "compact", 2, IncludesPromotions: false);

    [FrontendSelector("web")]
    private static bool IsWeb(CommerceClientRequest request) => request.Client.Equals("web", StringComparison.OrdinalIgnoreCase);

    [FrontendHandler("web")]
    private static CommerceClientResponse Web(BackendsForFrontendsContext<CommerceClientRequest> ctx)
        => new(ctx.Request.CustomerId, "rich", 2, IncludesPromotions: true);

    [FrontendFallback]
    private static CommerceClientResponse Standard(BackendsForFrontendsContext<CommerceClientRequest> ctx)
        => new(ctx.Request.CustomerId, "standard", 2, IncludesPromotions: true);
}

public sealed class CommerceBackendsForFrontendsDemoRunner(CommerceBackendsForFrontendsService service)
{
    public CommerceClientResponse RunGenerated(string client, string customerId) => service.GetSummary(client, customerId);

    public static CommerceClientResponse RunFluent()
    {
        var bff = CommerceBackendsForFrontends.CreateFluent(new DemoCommerceSummaryBackend());
        var result = bff.Dispatch(new CommerceClientRequest("web", "C-100"));
        return result.Response!;
    }
}

public static class CommerceBackendsForFrontendsServiceCollectionExtensions
{
    public static IServiceCollection AddCommerceBackendsForFrontendsDemo(this IServiceCollection services)
    {
        services.AddSingleton<ICommerceSummaryBackend, DemoCommerceSummaryBackend>();
        services.AddSingleton(static _ => GeneratedCommerceBackendsForFrontends.Create());
        services.AddSingleton<CommerceBackendsForFrontendsService>();
        services.AddSingleton<CommerceBackendsForFrontendsDemoRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapCommerceBackendsForFrontends(this IEndpointRouteBuilder endpoints, string pattern = "/commerce/{client}/{customerId}/summary")
    {
        endpoints.MapGet(pattern, (string client, string customerId, CommerceBackendsForFrontendsService service) => Results.Ok(service.GetSummary(client, customerId)))
            .WithName("CommerceBackendsForFrontends");
        return endpoints;
    }
}
