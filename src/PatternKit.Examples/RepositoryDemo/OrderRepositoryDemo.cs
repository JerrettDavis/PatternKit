using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.Repository;
using PatternKit.Application.Specification;
using PatternKit.Generators.Repository;

namespace PatternKit.Examples.RepositoryDemo;

/// <summary>Production-shaped repository example for order persistence boundaries.</summary>
public static class OrderRepositoryDemo
{
    public static async ValueTask<OrderRepositorySummary> RunFluentAsync()
    {
        var repository = OrderRepositoryPolicies.CreateFluentRepository();
        return await SeedAndQueryAsync(repository);
    }

    public static async ValueTask<OrderRepositorySummary> RunGeneratedAsync()
    {
        var repository = GeneratedOrderRepository.CreateRepository();
        return await SeedAndQueryAsync(repository);
    }

    internal static async ValueTask<OrderRepositorySummary> SeedAndQueryAsync(IRepository<OrderRecord, string> repository)
    {
        var pending = Specification<OrderRecord>.Where("pending", static order => order.Status == "Pending");

        var first = await repository.AddAsync(new OrderRecord("order-100", "customer-1", "Pending", 125m));
        _ = await repository.AddAsync(new OrderRecord("order-101", "customer-2", "Closed", 25m));
        var duplicate = await repository.AddAsync(new OrderRecord("order-100", "customer-1", "Pending", 125m));
        var pendingOrders = await repository.FindAsync(pending);
        var loaded = await repository.GetAsync("order-100");

        return new OrderRepositorySummary(
            first.Succeeded,
            duplicate.Status == RepositoryStatus.Conflict,
            pendingOrders.Count,
            loaded?.CustomerId ?? string.Empty);
    }
}

public sealed record OrderRecord(string OrderId, string CustomerId, string Status, decimal Total);

public sealed record OrderRepositorySummary(
    bool Stored,
    bool DuplicateRejected,
    int PendingCount,
    string LoadedCustomerId);

public static class OrderRepositoryPolicies
{
    public static InMemoryRepository<OrderRecord, string> CreateFluentRepository()
        => InMemoryRepository<OrderRecord, string>.Create(static order => order.OrderId)
            .UseComparer(StringComparer.OrdinalIgnoreCase)
            .Build();
}

public sealed class OrderRepositoryWorkflow
{
    private readonly IRepository<OrderRecord, string> _repository;

    public OrderRepositoryWorkflow(IRepository<OrderRecord, string> repository)
    {
        _repository = repository;
    }

    public ValueTask<OrderRepositorySummary> RunAsync()
        => OrderRepositoryDemo.SeedAndQueryAsync(_repository);
}

public sealed record OrderRepositoryDemoRunner(
    Func<ValueTask<OrderRepositorySummary>> RunFluentAsync,
    Func<ValueTask<OrderRepositorySummary>> RunGeneratedAsync);

public static class OrderRepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddOrderRepositoryDemo(this IServiceCollection services)
    {
        services.AddSingleton<IRepository<OrderRecord, string>>(_ => OrderRepositoryPolicies.CreateFluentRepository());
        services.AddSingleton<OrderRepositoryWorkflow>();
        services.AddSingleton(new OrderRepositoryDemoRunner(OrderRepositoryDemo.RunFluentAsync, OrderRepositoryDemo.RunGeneratedAsync));
        return services;
    }
}

[GenerateRepository(typeof(OrderRecord), typeof(string), FactoryName = "CreateRepository")]
public static partial class GeneratedOrderRepository
{
    [RepositoryKeySelector]
    private static string SelectKey(OrderRecord order) => order.OrderId;
}
