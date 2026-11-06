using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.CompetingConsumers;

namespace PatternKit.Examples.Messaging;

public sealed record FulfillmentConsumerWork(string OrderId, string Region);

public sealed record FulfillmentConsumerResult(string OrderId, string Region, string ConsumerName, bool Accepted);

public interface IFulfillmentConsumer
{
    string Name { get; }

    ValueTask<FulfillmentConsumerResult> HandleAsync(FulfillmentConsumerWork work, CancellationToken cancellationToken = default);
}

public sealed class RegionalFulfillmentConsumer(string name) : IFulfillmentConsumer
{
    public string Name { get; } = name;

    public ValueTask<FulfillmentConsumerResult> HandleAsync(FulfillmentConsumerWork work, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new FulfillmentConsumerResult(work.OrderId, work.Region, Name, true));
    }
}

public sealed class FulfillmentCompetingConsumerService(
    CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult> consumers)
{
    public ValueTask<CompetingConsumerResult<FulfillmentConsumerResult>> DispatchAsync(
        FulfillmentConsumerWork work,
        CancellationToken cancellationToken = default)
        => consumers.DispatchAsync(work, cancellationToken);
}

public static class FulfillmentCompetingConsumersExample
{
    public static async ValueTask<FulfillmentCompetingConsumersSummary> RunFluentAsync()
        => await RunScenarioAsync(FulfillmentCompetingConsumerGroups.CreateFluentGroup());

    public static async ValueTask<FulfillmentCompetingConsumersSummary> RunGeneratedAsync()
        => await RunScenarioAsync(FulfillmentCompetingConsumerGroups.CreateGeneratedGroup());

    private static async ValueTask<FulfillmentCompetingConsumersSummary> RunScenarioAsync(
        CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult> group)
    {
        var service = new FulfillmentCompetingConsumerService(group);
        var first = await service.DispatchAsync(new FulfillmentConsumerWork("order-200", "east"));
        var second = await service.DispatchAsync(new FulfillmentConsumerWork("order-201", "west"));
        var consumers = new[] { first.ConsumerName!, second.ConsumerName! };
        return new FulfillmentCompetingConsumersSummary(group.Name, first.Accepted && second.Accepted, consumers);
    }
}

public sealed record FulfillmentCompetingConsumersSummary(
    string GroupName,
    bool Accepted,
    IReadOnlyList<string> Consumers);

public static class FulfillmentCompetingConsumerGroups
{
    public static CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult> CreateFluentGroup()
        => CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult>
            .Create("fulfillment-competing-consumers")
            .WithMaxConcurrentDeliveries(2)
            .AddConsumer("east-worker", (work, cancellationToken) => new RegionalFulfillmentConsumer("east-worker").HandleAsync(work, cancellationToken))
            .AddConsumer("west-worker", (work, cancellationToken) => new RegionalFulfillmentConsumer("west-worker").HandleAsync(work, cancellationToken))
            .Build();

    public static CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult> CreateGeneratedGroup()
        => GeneratedFulfillmentCompetingConsumers.CreateGroup()
            .AddConsumer("east-worker", (work, cancellationToken) => new RegionalFulfillmentConsumer("east-worker").HandleAsync(work, cancellationToken))
            .AddConsumer("west-worker", (work, cancellationToken) => new RegionalFulfillmentConsumer("west-worker").HandleAsync(work, cancellationToken))
            .Build();
}

[GenerateCompetingConsumerGroup(
    typeof(FulfillmentConsumerWork),
    typeof(FulfillmentConsumerResult),
    FactoryMethodName = "CreateGroup",
    GroupName = "fulfillment-competing-consumers",
    MaxConcurrentDeliveries = 2)]
public static partial class GeneratedFulfillmentCompetingConsumers;

public sealed record FulfillmentCompetingConsumersRunner(
    Func<ValueTask<FulfillmentCompetingConsumersSummary>> RunFluentAsync,
    Func<ValueTask<FulfillmentCompetingConsumersSummary>> RunGeneratedAsync);

public static class FulfillmentCompetingConsumersServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentCompetingConsumersDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => FulfillmentCompetingConsumerGroups.CreateGeneratedGroup());
        services.AddSingleton<FulfillmentCompetingConsumerService>();
        services.AddSingleton(new FulfillmentCompetingConsumersRunner(
            FulfillmentCompetingConsumersExample.RunFluentAsync,
            FulfillmentCompetingConsumersExample.RunGeneratedAsync));
        return services;
    }
}
