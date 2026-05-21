using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.PipesAndFilters;

namespace PatternKit.Examples.Messaging;

public sealed record FulfillmentPipelineContext(string OrderId, bool Validated, bool Reserved, bool Published);

public sealed record FulfillmentPipesAndFiltersSummary(string PipelineName, bool Published, IReadOnlyList<string> Filters);

public sealed class FulfillmentPipesAndFiltersService(PipesAndFiltersPipeline<FulfillmentPipelineContext> pipeline)
{
    public ValueTask<PipesAndFiltersResult<FulfillmentPipelineContext>> ProcessAsync(string orderId, CancellationToken cancellationToken = default)
        => pipeline.ExecuteAsync(new FulfillmentPipelineContext(orderId, false, false, false), cancellationToken);
}

public static class FulfillmentPipesAndFiltersExample
{
    public static async ValueTask<FulfillmentPipesAndFiltersSummary> RunFluentAsync()
        => await RunScenarioAsync(FulfillmentPipesAndFiltersPipelines.CreateFluentPipeline());

    public static async ValueTask<FulfillmentPipesAndFiltersSummary> RunGeneratedAsync()
        => await RunScenarioAsync(FulfillmentPipesAndFiltersPipelines.CreateGeneratedPipeline());

    private static async ValueTask<FulfillmentPipesAndFiltersSummary> RunScenarioAsync(PipesAndFiltersPipeline<FulfillmentPipelineContext> pipeline)
    {
        var result = await pipeline.ExecuteAsync(new FulfillmentPipelineContext("order-300", false, false, false));
        return new FulfillmentPipesAndFiltersSummary(
            pipeline.Name,
            result.Value.Published,
            result.Filters.Select(static filter => filter.Name).ToArray());
    }
}

public static class FulfillmentPipesAndFiltersPipelines
{
    public static PipesAndFiltersPipeline<FulfillmentPipelineContext> CreateFluentPipeline()
        => PipesAndFiltersPipeline<FulfillmentPipelineContext>
            .Create("fulfillment-pipes")
            .AddFilter("validate", (ctx, _) => new ValueTask<FulfillmentPipelineContext>(ctx with { Validated = true }))
            .AddFilter("reserve", (ctx, _) => new ValueTask<FulfillmentPipelineContext>(ctx with { Reserved = ctx.Validated }))
            .AddFilter("publish", (ctx, _) => new ValueTask<FulfillmentPipelineContext>(ctx with { Published = ctx.Reserved }))
            .Build();

    public static PipesAndFiltersPipeline<FulfillmentPipelineContext> CreateGeneratedPipeline()
        => GeneratedFulfillmentPipesAndFilters.CreatePipeline()
            .AddFilter("validate", (ctx, _) => new ValueTask<FulfillmentPipelineContext>(ctx with { Validated = true }))
            .AddFilter("reserve", (ctx, _) => new ValueTask<FulfillmentPipelineContext>(ctx with { Reserved = ctx.Validated }))
            .AddFilter("publish", (ctx, _) => new ValueTask<FulfillmentPipelineContext>(ctx with { Published = ctx.Reserved }))
            .Build();
}

[GeneratePipesAndFiltersPipeline(
    typeof(FulfillmentPipelineContext),
    FactoryMethodName = "CreatePipeline",
    PipelineName = "fulfillment-pipes")]
public static partial class GeneratedFulfillmentPipesAndFilters;

public sealed record FulfillmentPipesAndFiltersRunner(
    Func<ValueTask<FulfillmentPipesAndFiltersSummary>> RunFluentAsync,
    Func<ValueTask<FulfillmentPipesAndFiltersSummary>> RunGeneratedAsync);

public static class FulfillmentPipesAndFiltersServiceCollectionExtensions
{
    public static IServiceCollection AddFulfillmentPipesAndFiltersDemo(this IServiceCollection services)
    {
        services.AddSingleton(static _ => FulfillmentPipesAndFiltersPipelines.CreateGeneratedPipeline());
        services.AddSingleton<FulfillmentPipesAndFiltersService>();
        services.AddSingleton(new FulfillmentPipesAndFiltersRunner(
            FulfillmentPipesAndFiltersExample.RunFluentAsync,
            FulfillmentPipesAndFiltersExample.RunGeneratedAsync));
        return services;
    }
}
