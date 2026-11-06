using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.PriorityQueue;
using PatternKit.Generators.PriorityQueue;

namespace PatternKit.Examples.PriorityQueueDemo;

public sealed record FulfillmentPriorityWork(string OrderId, string CustomerTier, bool Expedited);

public sealed record FulfillmentPrioritySummary(string QueueName, string FirstOrderId, int FirstPriority, int RemainingCount);

public sealed class FulfillmentPriorityQueueService(PriorityQueuePolicy<FulfillmentPriorityWork, int> queue)
{
    public FulfillmentPrioritySummary Schedule(params FulfillmentPriorityWork[] work)
    {
        if (work is null)
            throw new ArgumentNullException(nameof(work));

        foreach (var item in work)
            queue.Enqueue(item);

        var first = queue.Dequeue();
        if (!first.HasItem)
            throw new InvalidOperationException("Priority queue returned no work.");

        return new(first.QueueName, first.Item!.OrderId, first.Priority!, first.RemainingCount);
    }
}

public static class FulfillmentPriorityQueues
{
    public static PriorityQueuePolicy<FulfillmentPriorityWork, int> CreateFluent()
        => PriorityQueuePolicy<FulfillmentPriorityWork, int>.Create("fulfillment-priority")
            .WithPrioritySelector(GetPriority)
            .DequeueHighestPriorityFirst()
            .Build();

    public static int GetPriority(FulfillmentPriorityWork work)
    {
        var tierPriority = work.CustomerTier.Equals("enterprise", StringComparison.OrdinalIgnoreCase) ? 20 : 5;
        var expeditePriority = work.Expedited ? 10 : 0;
        return tierPriority + expeditePriority;
    }
}

[GeneratePriorityQueue(typeof(FulfillmentPriorityWork), typeof(int), FactoryMethodName = "Create", QueueName = "fulfillment-priority")]
public static partial class GeneratedFulfillmentPriorityQueue
{
    [PriorityQueuePrioritySelector]
    private static int GetPriority(FulfillmentPriorityWork work) => FulfillmentPriorityQueues.GetPriority(work);
}

public sealed class FulfillmentPriorityQueueDemoRunner(FulfillmentPriorityQueueService service)
{
    public FulfillmentPrioritySummary RunGenerated(params FulfillmentPriorityWork[] work) => service.Schedule(work);

    public static FulfillmentPrioritySummary RunFluent()
        => RunWith(FulfillmentPriorityQueues.CreateFluent());

    public static FulfillmentPrioritySummary RunGeneratedStatic()
        => RunWith(GeneratedFulfillmentPriorityQueue.Create());

    private static FulfillmentPrioritySummary RunWith(PriorityQueuePolicy<FulfillmentPriorityWork, int> queue)
        => new FulfillmentPriorityQueueService(queue).Schedule(
            new FulfillmentPriorityWork("order-standard", "standard", false),
            new FulfillmentPriorityWork("order-expedited", "standard", true),
            new FulfillmentPriorityWork("order-enterprise", "enterprise", false));
}

public static class FulfillmentPriorityQueueServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentPriorityQueueDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => GeneratedFulfillmentPriorityQueue.Create());
        services.AddSingleton<FulfillmentPriorityQueueService>();
        services.AddSingleton<FulfillmentPriorityQueueDemoRunner>();
        return services;
    }
}
