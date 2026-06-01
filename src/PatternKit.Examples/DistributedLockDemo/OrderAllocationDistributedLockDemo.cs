using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.DistributedLocks;
using PatternKit.Generators.DistributedLocks;

namespace PatternKit.Examples.DistributedLockDemo;

public sealed record OrderAllocationRequest(string OrderId, string WorkerId, int Quantity);

public sealed record OrderAllocationSummary(string OrderId, string WorkerId, bool Acquired, bool Released, bool BlockedWhileActive);

public sealed class OrderAllocationLockWorkflow(DistributedLock<string> distributedLock)
{
    public OrderAllocationSummary Allocate(OrderAllocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var acquired = distributedLock.TryAcquire(request.OrderId, request.WorkerId);
        if (!acquired.Acquired)
            return new(request.OrderId, request.WorkerId, false, false, true);

        var blocked = distributedLock.TryAcquire(request.OrderId, "competing-worker");
        var released = distributedLock.Release(acquired.Lease!);
        return new(request.OrderId, request.WorkerId, true, released.Released, blocked.Failed);
    }
}

public static class OrderAllocationDistributedLocks
{
    public static DistributedLock<string> CreateFluent(Func<DateTimeOffset>? clock = null)
        => DistributedLock<string>.Create("order-allocation-lock")
            .LeaseDuration(TimeSpan.FromSeconds(30))
            .WithClock(clock ?? (() => DateTimeOffset.UtcNow))
            .Build();
}

[GenerateDistributedLock(typeof(string), FactoryMethodName = "Create", LockName = "order-allocation-lock", LeaseDurationMilliseconds = 30000)]
public static partial class GeneratedOrderAllocationDistributedLock;

public sealed class OrderAllocationDistributedLockDemoRunner(OrderAllocationLockWorkflow workflow)
{
    public OrderAllocationSummary RunGenerated()
        => workflow.Allocate(new("ORDER-100", "allocator-a", 4));

    public static OrderAllocationSummary RunFluent()
    {
        var workflow = new OrderAllocationLockWorkflow(OrderAllocationDistributedLocks.CreateFluent());
        return workflow.Allocate(new("ORDER-100", "allocator-a", 4));
    }
}

public static class OrderAllocationDistributedLockServiceCollectionExtensions
{
    public static IServiceCollection AddOrderAllocationDistributedLockDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedOrderAllocationDistributedLock.Create());
        services.AddSingleton<OrderAllocationLockWorkflow>();
        services.AddSingleton<OrderAllocationDistributedLockDemoRunner>();
        return services;
    }
}
