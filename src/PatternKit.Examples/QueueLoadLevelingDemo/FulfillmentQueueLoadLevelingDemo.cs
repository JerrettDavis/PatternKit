using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.QueueLoadLeveling;
using PatternKit.Generators.QueueLoadLeveling;

namespace PatternKit.Examples.QueueLoadLevelingDemo;

public sealed record FulfillmentWorkItem(string OrderId, string Warehouse);

public sealed record FulfillmentQueueResult(string OrderId, bool Accepted, bool Rejected, bool Queued, string Worker);

public interface IFulfillmentWorker
{
    ValueTask<FulfillmentQueueResult> ProcessAsync(FulfillmentWorkItem item, CancellationToken cancellationToken = default);
}

public sealed class ScriptedFulfillmentWorker : IFulfillmentWorker
{
    public int Calls { get; private set; }

    public ValueTask<FulfillmentQueueResult> ProcessAsync(FulfillmentWorkItem item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return new(new FulfillmentQueueResult(item.OrderId, true, false, false, $"worker-{Calls}"));
    }
}

public sealed class FulfillmentQueueLoadLevelingService(
    IFulfillmentWorker worker,
    QueueLoadLevelingPolicy<FulfillmentQueueResult> policy)
{
    public async ValueTask<FulfillmentQueueResult> EnqueueAsync(FulfillmentWorkItem item, CancellationToken cancellationToken = default)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        var result = await policy.ExecuteAsync(
            ct => worker.ProcessAsync(item, ct),
            cancellationToken);

        return result.Value is not null
            ? result.Value with { Queued = result.Queued }
            : new FulfillmentQueueResult(item.OrderId, false, result.Rejected, result.Queued, "");
    }
}

public static class FulfillmentQueueLoadLevelingDemo
{
    public static async ValueTask<FulfillmentQueueSummary> RunFluentAsync()
        => await RunScenarioAsync(FulfillmentQueueLoadLevelingPolicies.CreateFluentPolicy());

    public static async ValueTask<FulfillmentQueueSummary> RunGeneratedAsync()
        => await RunScenarioAsync(GeneratedFulfillmentQueueLoadLevelingPolicy.CreatePolicy());

    private static async ValueTask<FulfillmentQueueSummary> RunScenarioAsync(QueueLoadLevelingPolicy<FulfillmentQueueResult> policy)
    {
        var worker = new ScriptedFulfillmentWorker();
        var service = new FulfillmentQueueLoadLevelingService(worker, policy);
        var first = await service.EnqueueAsync(new FulfillmentWorkItem("order-100", "central"));
        var second = await service.EnqueueAsync(new FulfillmentWorkItem("order-101", "central"));
        return new FulfillmentQueueSummary(policy.Name, first.Accepted && second.Accepted, worker.Calls, second.Worker);
    }
}

public sealed record FulfillmentQueueSummary(string PolicyName, bool Accepted, int ProcessedCount, string LastWorker);

public static class FulfillmentQueueLoadLevelingPolicies
{
    public static QueueLoadLevelingPolicy<FulfillmentQueueResult> CreateFluentPolicy()
        => QueueLoadLevelingPolicy<FulfillmentQueueResult>
            .Create("fulfillment-queue")
            .WithMaxConcurrentWorkers(2)
            .WithMaxQueueLength(32)
            .WithQueueTimeout(TimeSpan.FromMilliseconds(500))
            .Build();
}

[GenerateQueueLoadLevelingPolicy(
    typeof(FulfillmentQueueResult),
    FactoryMethodName = "CreatePolicy",
    PolicyName = "fulfillment-queue",
    MaxConcurrentWorkers = 2,
    MaxQueueLength = 32,
    QueueTimeoutMilliseconds = 500)]
public static partial class GeneratedFulfillmentQueueLoadLevelingPolicy;

public sealed record FulfillmentQueueLoadLevelingDemoRunner(
    Func<ValueTask<FulfillmentQueueSummary>> RunFluentAsync,
    Func<ValueTask<FulfillmentQueueSummary>> RunGeneratedAsync);

public static class FulfillmentQueueLoadLevelingServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentQueueLoadLevelingDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedFulfillmentQueueLoadLevelingPolicy.CreatePolicy());
        services.AddSingleton<ScriptedFulfillmentWorker>();
        services.AddSingleton<IFulfillmentWorker>(static sp => sp.GetRequiredService<ScriptedFulfillmentWorker>());
        services.AddSingleton<FulfillmentQueueLoadLevelingService>();
        services.AddSingleton(new FulfillmentQueueLoadLevelingDemoRunner(
            FulfillmentQueueLoadLevelingDemo.RunFluentAsync,
            FulfillmentQueueLoadLevelingDemo.RunGeneratedAsync));
        return services;
    }
}
