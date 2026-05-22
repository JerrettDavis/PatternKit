using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.GatewayAggregation;
using PatternKit.Generators.GatewayAggregation;

namespace PatternKit.Examples.GatewayAggregationDemo;

public sealed record CustomerDashboardRequest(string CustomerId);

public sealed record CustomerProfile(string CustomerId, string Name, string Tier);

public sealed record CustomerOrderSummary(string CustomerId, int OpenOrders);

public sealed record CustomerRecommendationSummary(string CustomerId, int RecommendedProducts);

public sealed record CustomerDashboardResponse(string CustomerId, string Name, string Tier, int OpenOrders, int RecommendedProducts);

public interface ICustomerProfileClient
{
    CustomerProfile GetProfile(CustomerDashboardRequest request);
}

public interface ICustomerOrdersClient
{
    CustomerOrderSummary GetOrders(CustomerDashboardRequest request);
}

public interface ICustomerRecommendationClient
{
    CustomerRecommendationSummary GetRecommendations(CustomerDashboardRequest request);
}

public sealed class DemoCustomerProfileClient : ICustomerProfileClient
{
    public CustomerProfile GetProfile(CustomerDashboardRequest request) => new(request.CustomerId, "Ada Lovelace", "gold");
}

public sealed class DemoCustomerOrdersClient : ICustomerOrdersClient
{
    public CustomerOrderSummary GetOrders(CustomerDashboardRequest request) => new(request.CustomerId, 2);
}

public sealed class DemoCustomerRecommendationClient : ICustomerRecommendationClient
{
    public CustomerRecommendationSummary GetRecommendations(CustomerDashboardRequest request) => new(request.CustomerId, 4);
}

public sealed class CustomerDashboardGatewayService(GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse> gateway)
{
    public CustomerDashboardResponse GetDashboard(string customerId)
    {
        var result = gateway.Aggregate(new CustomerDashboardRequest(customerId));
        if (result.Failed)
            throw new InvalidOperationException("Customer dashboard could not be aggregated.", result.Exception);

        return result.Response!;
    }
}

public static class CustomerDashboardGateways
{
    public static GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse> CreateFluent(
        ICustomerProfileClient profiles,
        ICustomerOrdersClient orders,
        ICustomerRecommendationClient recommendations)
        => GatewayAggregation<CustomerDashboardRequest, CustomerDashboardResponse>.Create("customer-dashboard")
            .Fetch<CustomerProfile>("profile", profiles.GetProfile)
            .Fetch<CustomerOrderSummary>("orders", orders.GetOrders)
            .Fetch<CustomerRecommendationSummary>("recommendations", recommendations.GetRecommendations)
            .Compose(Compose)
            .Build();

    public static CustomerDashboardResponse Compose(GatewayAggregationContext<CustomerDashboardRequest> ctx)
    {
        var profile = ctx.Require<CustomerProfile>("profile");
        var orders = ctx.Require<CustomerOrderSummary>("orders");
        var recommendations = ctx.Require<CustomerRecommendationSummary>("recommendations");
        return new(profile.CustomerId, profile.Name, profile.Tier, orders.OpenOrders, recommendations.RecommendedProducts);
    }
}

[GenerateGatewayAggregation(typeof(CustomerDashboardRequest), typeof(CustomerDashboardResponse), FactoryMethodName = "Create", GatewayName = "customer-dashboard")]
public static partial class GeneratedCustomerDashboardGateway
{
    [GatewayAggregationFetch("profile")]
    private static CustomerProfile Profile(CustomerDashboardRequest request) => new(request.CustomerId, "Ada Lovelace", "gold");

    [GatewayAggregationFetch("orders")]
    private static CustomerOrderSummary Orders(CustomerDashboardRequest request) => new(request.CustomerId, 2);

    [GatewayAggregationFetch("recommendations")]
    private static CustomerRecommendationSummary Recommendations(CustomerDashboardRequest request) => new(request.CustomerId, 4);

    [GatewayAggregationComposer]
    private static CustomerDashboardResponse Compose(GatewayAggregationContext<CustomerDashboardRequest> ctx)
        => CustomerDashboardGateways.Compose(ctx);
}

public sealed class CustomerDashboardGatewayAggregationDemoRunner(CustomerDashboardGatewayService service)
{
    public CustomerDashboardResponse RunGenerated(string customerId) => service.GetDashboard(customerId);

    public static CustomerDashboardResponse RunFluent()
    {
        var gateway = CustomerDashboardGateways.CreateFluent(
            new DemoCustomerProfileClient(),
            new DemoCustomerOrdersClient(),
            new DemoCustomerRecommendationClient());
        var result = gateway.Aggregate(new CustomerDashboardRequest("C-100"));
        return result.Response!;
    }
}

public static class CustomerDashboardGatewayAggregationServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerDashboardGatewayAggregationDemo(this IServiceCollection services)
    {
        services.AddSingleton<ICustomerProfileClient, DemoCustomerProfileClient>();
        services.AddSingleton<ICustomerOrdersClient, DemoCustomerOrdersClient>();
        services.AddSingleton<ICustomerRecommendationClient, DemoCustomerRecommendationClient>();
        services.AddSingleton(static _ => GeneratedCustomerDashboardGateway.Create());
        services.AddSingleton<CustomerDashboardGatewayService>();
        services.AddSingleton<CustomerDashboardGatewayAggregationDemoRunner>();
        return services;
    }

    public static IEndpointRouteBuilder MapCustomerDashboardGatewayAggregation(this IEndpointRouteBuilder endpoints, string pattern = "/customers/{customerId}/dashboard")
    {
        endpoints.MapGet(pattern, (string customerId, CustomerDashboardGatewayService service) => Results.Ok(service.GetDashboard(customerId)))
            .WithName("CustomerDashboardGatewayAggregation");
        return endpoints;
    }
}
