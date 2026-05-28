using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.Bulkhead;
using PatternKit.Cloud.CircuitBreaker;
using PatternKit.Cloud.PriorityQueue;
using PatternKit.Cloud.QueueLoadLeveling;
using PatternKit.Cloud.RateLimiting;
using PatternKit.Cloud.Retry;
using PatternKit.Hosting.DependencyInjection;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Reliability;
using PatternKit.Messaging.Storage;

namespace PatternKit.Benchmarks.Hosting;

[BenchmarkCategory("Hosting", "IServiceCollection")]
public class HostingExtensionsBenchmarks
{
    private ServiceProvider _provider = default!;

    [GlobalSetup]
    public void Setup()
    {
        _provider = CreateConfiguredServices().BuildServiceProvider(validateScopes: true);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _provider.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Hosting: register reusable PatternKit primitives")]
    [BenchmarkCategory("Construction", "Registration")]
    public IServiceCollection Construction_RegisterReusablePrimitives()
        => CreateConfiguredServices();

    [Benchmark(Description = "Hosting: build provider for reusable PatternKit primitives")]
    [BenchmarkCategory("Construction", "Provider")]
    public ServiceProvider Construction_BuildProvider()
        => CreateConfiguredServices().BuildServiceProvider(validateScopes: true);

    [Benchmark(Description = "Hosting: resolve and execute reusable PatternKit primitives")]
    [BenchmarkCategory("Execution", "Resolve")]
    public async Task<int> Execution_ResolveAndExecuteAsync()
    {
        var channel = _provider.GetRequiredService<MessageChannel<OrderCommand>>();
        var store = _provider.GetRequiredService<MessageStore<OrderCommand>>();
        var delivery = _provider.GetRequiredService<GuaranteedDeliveryQueue<OrderCommand>>();
        var retry = _provider.GetRequiredService<RetryPolicy<ServiceReply>>();
        var breaker = _provider.GetRequiredService<CircuitBreakerPolicy<ServiceReply>>();
        var bulkhead = _provider.GetRequiredService<BulkheadPolicy<ServiceReply>>();
        var rateLimit = _provider.GetRequiredService<RateLimitPolicy<ServiceReply>>();
        var leveling = _provider.GetRequiredService<QueueLoadLevelingPolicy<ServiceReply>>();
        var priority = _provider.GetRequiredService<PriorityQueuePolicy<WorkItem, int>>();

        var message = Message<OrderCommand>.Create(new("order-1", 125m));
        var send = channel.Send(message);
        var append = store.Append(message);
        await delivery.EnqueueAsync(message);
        var lease = await delivery.TryReceiveAsync();

        var retryResult = retry.Execute(static () => new ServiceReply(true));
        var breakerResult = breaker.Execute(static () => new ServiceReply(true));
        var bulkheadResult = bulkhead.Execute(static () => new ServiceReply(true));
        var rateLimitResult = rateLimit.Execute("tenant-a", static () => new ServiceReply(true));
        var levelingResult = leveling.Execute(static () => new ServiceReply(true));
        priority.Enqueue(new("slow", 1));
        priority.Enqueue(new("fast", 10));
        var next = priority.Dequeue();

        return (send.Accepted ? 1 : 0)
            + append.StoredMessage.Message.Payload.OrderId.Length
            + (lease is null ? 0 : 1)
            + (retryResult.Succeeded ? 1 : 0)
            + (breakerResult.Succeeded ? 1 : 0)
            + (bulkheadResult.Succeeded ? 1 : 0)
            + (rateLimitResult.Allowed ? 1 : 0)
            + (levelingResult.Accepted ? 1 : 0)
            + (next.Item?.Priority ?? 0);
    }

    private static IServiceCollection CreateConfiguredServices()
    {
        var services = new ServiceCollection();
        services
            .AddPatternKitMessageChannel<OrderCommand>(
                "orders",
                builder => builder.WithCapacity(32, MessageChannelBackpressurePolicy.Reject))
            .AddPatternKitMessageStore<OrderCommand>(
                "order-store",
                builder => builder.IdentifyBy(static (message, _) => message.Payload.OrderId))
            .AddPatternKitGuaranteedDelivery<OrderCommand>(
                builder => builder
                    .Name("order-delivery")
                    .LeaseDuration(TimeSpan.FromSeconds(5))
                    .MaxDeliveryAttempts(2))
            .AddPatternKitRetryPolicy<ServiceReply>(
                "inventory-retry",
                builder => builder.WithMaxAttempts(2).HandleResult(static reply => !reply.Available))
            .AddPatternKitCircuitBreakerPolicy<ServiceReply>(
                "inventory-breaker",
                builder => builder.WithFailureThreshold(1).HandleResult(static reply => !reply.Available))
            .AddPatternKitBulkheadPolicy<ServiceReply>(
                "inventory-bulkhead",
                builder => builder.WithMaxConcurrency(2))
            .AddPatternKitRateLimitPolicy<ServiceReply>(
                "inventory-rate-limit",
                builder => builder.WithPermitLimit(64).WithWindow(TimeSpan.FromMinutes(1)))
            .AddPatternKitQueueLoadLevelingPolicy<ServiceReply>(
                "inventory-leveling",
                builder => builder.WithMaxConcurrentWorkers(2).WithMaxQueueLength(32))
            .AddPatternKitPriorityQueue<WorkItem, int>(
                static item => item.Priority,
                "inventory-priority");

        return services;
    }

    private sealed record OrderCommand(string OrderId, decimal Total);
    private sealed record ServiceReply(bool Available);
    private sealed record WorkItem(string Id, int Priority);
}
