using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.DomainServices;
using PatternKit.Generators.DomainServices;

namespace PatternKit.Examples.DomainServiceDemo;

/// <summary>
/// Production-style domain services for shipping choices that span order value and fulfillment constraints.
/// </summary>
public static class ShippingDomainServiceDemo
{
    public sealed record ShippingRequest(string OrderId, decimal Weight, decimal DeclaredValue, bool RequiresSignature);

    public sealed record ShippingDecision(string OrderId, string Carrier, decimal Cost, bool Insured);

    public static DomainServiceRegistry<ShippingRequest, ShippingDecision> CreateFluentRegistry()
        => DomainServiceRegistry<ShippingRequest, ShippingDecision>.Create()
            .Add("ground", static request => new ShippingDecision(
                request.OrderId,
                "ground",
                Math.Round(request.Weight * 1.25m + (request.RequiresSignature ? 4m : 0m), 2),
                false))
            .Add("insured-air", static request => new ShippingDecision(
                request.OrderId,
                "air",
                Math.Round(request.Weight * 3m + request.DeclaredValue * 0.01m + (request.RequiresSignature ? 4m : 0m), 2),
                true))
            .Build();

    public static DomainServiceRegistry<ShippingRequest, ShippingDecision> CreateGeneratedRegistry()
        => GeneratedShippingDomainServices.Create();

    public static ShippingDecision SelectBest(ShippingRequest request, DomainServiceRegistry<ShippingRequest, ShippingDecision> registry)
    {
        var operation = request.DeclaredValue >= 500m || request.RequiresSignature ? "insured-air" : "ground";
        return registry.Execute(operation, request);
    }

    public static ShippingRequest CreateStandardRequest()
        => new("ORD-100", 10m, 250m, false);

    public static ShippingRequest CreateHighValueRequest()
        => new("ORD-200", 8m, 1_000m, true);

    public static IServiceCollection AddShippingDomainServiceDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => CreateGeneratedRegistry());
        services.AddSingleton<ShippingDomainService>();
        return services;
    }
}

public sealed class ShippingDomainService(DomainServiceRegistry<ShippingDomainServiceDemo.ShippingRequest, ShippingDomainServiceDemo.ShippingDecision> registry)
{
    public ShippingDomainServiceDemo.ShippingDecision SelectBest(ShippingDomainServiceDemo.ShippingRequest request)
        => ShippingDomainServiceDemo.SelectBest(request, registry);
}

[GenerateDomainServiceRegistry(typeof(ShippingDomainServiceDemo.ShippingRequest), typeof(ShippingDomainServiceDemo.ShippingDecision))]
public static partial class GeneratedShippingDomainServices
{
    [DomainServiceOperation("ground")]
    private static ShippingDomainServiceDemo.ShippingDecision Ground(ShippingDomainServiceDemo.ShippingRequest request)
        => new(
            request.OrderId,
            "ground",
            Math.Round(request.Weight * 1.25m + (request.RequiresSignature ? 4m : 0m), 2),
            false);

    [DomainServiceOperation("insured-air")]
    private static ShippingDomainServiceDemo.ShippingDecision InsuredAir(ShippingDomainServiceDemo.ShippingRequest request)
        => new(
            request.OrderId,
            "air",
            Math.Round(request.Weight * 3m + request.DeclaredValue * 0.01m + (request.RequiresSignature ? 4m : 0m), 2),
            true);
}
