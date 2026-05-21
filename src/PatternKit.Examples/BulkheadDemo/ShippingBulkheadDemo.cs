using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.Bulkhead;
using PatternKit.Generators.Bulkhead;

namespace PatternKit.Examples.BulkheadDemo;

public sealed record ShippingAllocation(string OrderId, string Carrier, bool Reserved);

public interface IShippingAllocator
{
    ValueTask<ShippingAllocation> ReserveAsync(string orderId, CancellationToken cancellationToken = default);
}

public sealed class ScriptedShippingAllocator(params ShippingAllocation[] allocations) : IShippingAllocator
{
    private readonly Queue<ShippingAllocation> _allocations = new(allocations);

    public int Calls { get; private set; }

    public ValueTask<ShippingAllocation> ReserveAsync(string orderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;

        if (_allocations.Count == 0)
            return new(new ShippingAllocation(orderId, "ground", true));

        return new(_allocations.Dequeue());
    }
}

public sealed class ShippingBulkheadService(
    IShippingAllocator allocator,
    BulkheadPolicy<ShippingAllocation> policy)
{
    public async ValueTask<ShippingBulkheadSubmission> ReserveAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        var result = await policy.ExecuteAsync(
            ct => allocator.ReserveAsync(orderId, ct),
            cancellationToken);

        return new ShippingBulkheadSubmission(
            orderId,
            result.Value?.Carrier ?? "",
            result.Value?.Reserved ?? false,
            result.Succeeded,
            result.Rejected,
            result.TimedOut,
            result.Queued);
    }
}

public sealed record ShippingBulkheadSubmission(
    string OrderId,
    string Carrier,
    bool Reserved,
    bool Succeeded,
    bool Rejected,
    bool TimedOut,
    bool Queued);

public static partial class ShippingBulkheadPolicies
{
    public static BulkheadPolicy<ShippingAllocation> CreateFluentPolicy()
        => BulkheadPolicy<ShippingAllocation>
            .Create("shipping-allocation")
            .WithMaxConcurrency(4)
            .WithMaxQueueLength(16)
            .WithQueueTimeout(TimeSpan.FromMilliseconds(250))
            .Build();
}

[GenerateBulkheadPolicy(
    typeof(ShippingAllocation),
    FactoryMethodName = "CreateGeneratedPolicy",
    PolicyName = "shipping-allocation",
    MaxConcurrency = 4,
    MaxQueueLength = 16,
    QueueTimeoutMilliseconds = 250)]
public static partial class GeneratedShippingBulkheadPolicy;

public static class ShippingBulkheadDemoServiceCollectionExtensions
{
    public static IServiceCollection AddShippingBulkheadDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedShippingBulkheadPolicy.CreateGeneratedPolicy());
        services.AddSingleton<ScriptedShippingAllocator>(static _ => new(
            new ShippingAllocation("ORDER-100", "ground", true),
            new ShippingAllocation("ORDER-101", "air", true)));
        services.AddSingleton<IShippingAllocator>(static sp => sp.GetRequiredService<ScriptedShippingAllocator>());
        services.AddSingleton<ShippingBulkheadService>();
        return services;
    }
}
