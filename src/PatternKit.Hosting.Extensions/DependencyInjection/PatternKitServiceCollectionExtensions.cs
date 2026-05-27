using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.Bulkhead;
using PatternKit.Cloud.CircuitBreaker;
using PatternKit.Cloud.PriorityQueue;
using PatternKit.Cloud.QueueLoadLeveling;
using PatternKit.Cloud.RateLimiting;
using PatternKit.Cloud.Retry;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Reliability;
using PatternKit.Messaging.Storage;

namespace PatternKit.Hosting.DependencyInjection;

/// <summary>
/// Registers PatternKit runtime primitives with Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class PatternKitServiceCollectionExtensions
{
    public static IServiceCollection AddPatternKitMessageChannel<TPayload>(
        this IServiceCollection services,
        string name = "message-channel",
        Action<MessageChannel<TPayload>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = MessageChannel<TPayload>.Create(name);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitMessageStore<TPayload>(
        this IServiceCollection services,
        string name = "message-store",
        Action<MessageStore<TPayload>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = MessageStore<TPayload>.Create(name);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitGuaranteedDelivery<TPayload>(
        this IServiceCollection services,
        Action<GuaranteedDeliveryQueue<TPayload>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        Func<IServiceProvider, IGuaranteedDeliveryStore<TPayload>>? storeFactory = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            provider =>
            {
                var store = storeFactory?.Invoke(provider) ?? new InMemoryGuaranteedDeliveryStore<TPayload>();
                var builder = GuaranteedDeliveryQueue<TPayload>.Create(store);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitRetryPolicy<TResult>(
        this IServiceCollection services,
        string name = "retry",
        Action<RetryPolicy<TResult>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = RetryPolicy<TResult>.Create(name);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitCircuitBreakerPolicy<TResult>(
        this IServiceCollection services,
        string name = "circuit-breaker",
        Action<CircuitBreakerPolicy<TResult>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = CircuitBreakerPolicy<TResult>.Create(name);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitBulkheadPolicy<TResult>(
        this IServiceCollection services,
        string name = "bulkhead",
        Action<BulkheadPolicy<TResult>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = BulkheadPolicy<TResult>.Create(name);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitRateLimitPolicy<TResult>(
        this IServiceCollection services,
        string name = "rate-limit",
        Action<RateLimitPolicy<TResult>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = RateLimitPolicy<TResult>.Create(name);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitQueueLoadLevelingPolicy<TResult>(
        this IServiceCollection services,
        string name = "queue-load-leveling",
        Action<QueueLoadLevelingPolicy<TResult>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = QueueLoadLevelingPolicy<TResult>.Create(name);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    public static IServiceCollection AddPatternKitPriorityQueue<TItem, TPriority>(
        this IServiceCollection services,
        Func<TItem, TPriority> prioritySelector,
        string name = "priority-queue",
        Action<PriorityQueuePolicy<TItem, TPriority>.Builder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (prioritySelector is null)
            throw new ArgumentNullException(nameof(prioritySelector));

        return services.AddPatternKitService(
            lifetime,
            _ =>
            {
                var builder = PriorityQueuePolicy<TItem, TPriority>.Create(name)
                    .WithPrioritySelector(prioritySelector);
                configure?.Invoke(builder);
                return builder.Build();
            });
    }

    private static IServiceCollection AddPatternKitService<TService>(
        this IServiceCollection services,
        ServiceLifetime lifetime,
        Func<IServiceProvider, TService> factory)
        where TService : class
    {
        if (!Enum.IsDefined(typeof(ServiceLifetime), lifetime))
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Service lifetime is not valid.");

        services.Add(ServiceDescriptor.Describe(typeof(TService), provider => factory(provider), lifetime));
        return services;
    }
}
