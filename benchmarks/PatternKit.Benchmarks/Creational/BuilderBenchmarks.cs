using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Creational.Builder;
using PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;

namespace PatternKit.Benchmarks.Creational;

[BenchmarkCategory("Creational", "GoF", "Builder")]
public class BuilderBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create builder")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MutableBuilder<BenchmarkEndpointOptions> Fluent_CreateBuilder()
        => MutableBuilder<BenchmarkEndpointOptions>
            .New(static () => new BenchmarkEndpointOptions())
            .With(static options => options.Route = "/orders")
            .With(static options => options.TimeoutMilliseconds = 250)
            .RequireNotEmpty(static options => options.Route, nameof(BenchmarkEndpointOptions.Route))
            .RequireRange(static options => options.TimeoutMilliseconds, 1, 1_000, nameof(BenchmarkEndpointOptions.TimeoutMilliseconds));

    [Benchmark(Description = "Generated: create builder")]
    [BenchmarkCategory("Generated", "Construction")]
    public CorporateApplicationBuilder Generated_CreateBuilder()
        => CorporateApplicationDemo.CreateBuilder();

    [Benchmark(Description = "Fluent: build endpoint options")]
    [BenchmarkCategory("Fluent", "Execution")]
    public BenchmarkEndpointOptions Fluent_BuildEndpointOptions()
        => Fluent_CreateBuilder().Build();

    [Benchmark(Description = "Generated: build corporate application")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<int> Generated_BuildCorporateApplication()
    {
        var app = await CorporateApplicationDemo.CreateBuilder()
            .ForEnvironment(CorporateEnvironment.Production)
            .EnableMessaging()
            .EnableJobs()
            .LoadSecrets()
            .RequireModules()
            .BuildAsync()
            .ConfigureAwait(false);

        var featureCount = app.Host.Services.GetService<INotificationPublisher>() is null ? 0 : app.Log.Count;
        app.Host.Dispose();
        return featureCount;
    }
}

public sealed class BenchmarkEndpointOptions
{
    public string Route { get; set; } = string.Empty;

    public int TimeoutMilliseconds { get; set; }
}
