using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.IdentityMap;
using PatternKit.Application.Repository;
using PatternKit.Generators.IdentityMap;

namespace PatternKit.Examples.IdentityMapDemo;

public static class OrderIdentityMapDemo
{
    public static OrderIdentityMapSummary RunFluent()
    {
        var map = OrderIdentityMapPolicies.CreateFluentMap();
        return LoadTwice(map);
    }

    public static OrderIdentityMapSummary RunGenerated()
    {
        var map = GeneratedOrderIdentityMap.CreateMap();
        return LoadTwice(map);
    }

    internal static OrderIdentityMapSummary LoadTwice(IIdentityMap<TrackedOrder, string> map)
    {
        var repository = InMemoryRepository<TrackedOrder, string>.Create(static order => order.OrderId).Build();
        _ = repository.AddAsync(new TrackedOrder("order-100", "customer-1", 125m)).AsTask().GetAwaiter().GetResult();

        var first = map.GetOrAdd("order-100", key => repository.GetAsync(key).AsTask().GetAwaiter().GetResult()!);
        var second = map.GetOrAdd("order-100", key => repository.GetAsync(key).AsTask().GetAwaiter().GetResult()!);
        var conflict = map.Track("order-100", new TrackedOrder("order-100", "customer-1", 999m));

        return new(ReferenceEquals(first, second), conflict.Status == IdentityMapStatus.Conflict, map.Count, second.Total);
    }
}

public sealed record TrackedOrder(string OrderId, string CustomerId, decimal Total);

public sealed record OrderIdentityMapSummary(bool ReusedInstance, bool DuplicateRejected, int TrackedCount, decimal Total);

public static class OrderIdentityMapPolicies
{
    public static IdentityMap<TrackedOrder, string> CreateFluentMap()
        => IdentityMap<TrackedOrder, string>.Create(static order => order.OrderId)
            .UseComparer(StringComparer.OrdinalIgnoreCase)
            .Build();
}

public sealed class OrderIdentityMapWorkflow
{
    private readonly IIdentityMap<TrackedOrder, string> _map;

    public OrderIdentityMapWorkflow(IIdentityMap<TrackedOrder, string> map)
    {
        _map = map;
    }

    public OrderIdentityMapSummary Run() => OrderIdentityMapDemo.LoadTwice(_map);
}

public sealed record OrderIdentityMapDemoRunner(Func<OrderIdentityMapSummary> RunFluent, Func<OrderIdentityMapSummary> RunGenerated);

public static class OrderIdentityMapServiceCollectionExtensions
{
    public static IServiceCollection AddOrderIdentityMapDemo(this IServiceCollection services)
    {
        services.AddScoped<IIdentityMap<TrackedOrder, string>>(_ => OrderIdentityMapPolicies.CreateFluentMap());
        services.AddScoped<OrderIdentityMapWorkflow>();
        services.AddSingleton(new OrderIdentityMapDemoRunner(OrderIdentityMapDemo.RunFluent, OrderIdentityMapDemo.RunGenerated));
        return services;
    }
}

[GenerateIdentityMap(typeof(TrackedOrder), typeof(string), FactoryName = "CreateMap")]
public static partial class GeneratedOrderIdentityMap
{
    [IdentityMapKeySelector]
    private static string SelectKey(TrackedOrder order) => order.OrderId;
}
