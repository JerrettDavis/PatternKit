using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Creational.Factory;
using PatternKit.Examples.Generators.Factories;

namespace PatternKit.Benchmarks.Creational;

[BenchmarkCategory("Creational", "GoF", "FactoryMethod")]
public class FactoryMethodBenchmarks
{
    private static readonly string[] Modules = ["metrics", "workers"];

    [Benchmark(Baseline = true, Description = "Fluent: create factory method")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Factory<string, IServiceCollection, IServiceCollection> Fluent_CreateFactoryMethod()
        => Factory<string, IServiceCollection, IServiceCollection>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("metrics", static (in IServiceCollection services) =>
            {
                services.AddSingleton<IMetricsSink, ConsoleMetricsSink>();
                return services;
            })
            .Map("caching", static (in IServiceCollection services) =>
            {
                services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
                return services;
            })
            .Map("workers", static (in IServiceCollection services) =>
            {
                services.AddSingleton<IWorker, BackgroundWorker>();
                return services;
            })
            .Default(static (in IServiceCollection services) =>
            {
                services.AddSingleton<IMetricsSink, ConsoleMetricsSink>();
                services.AddSingleton<IWorker, BackgroundWorker>();
                return services;
            })
            .Build();

    [Benchmark(Description = "Generated: resolve factory method surface")]
    [BenchmarkCategory("Generated", "Construction")]
    public Type Generated_ResolveFactoryMethodSurface()
        => typeof(ServiceModules);

    [Benchmark(Description = "Fluent: configure service modules")]
    [BenchmarkCategory("Fluent", "Execution")]
    public int Fluent_ConfigureServiceModules()
    {
        var services = new ServiceCollection();
        var factory = Fluent_CreateFactoryMethod();
        foreach (var module in Modules)
            factory.Create(module, services);

        using var provider = services.BuildServiceProvider();
        return provider.GetServices<IWorker>().Count() + provider.GetServices<IMetricsSink>().Count();
    }

    [Benchmark(Description = "Generated: configure service modules")]
    [BenchmarkCategory("Generated", "Execution")]
    public int Generated_ConfigureServiceModules()
    {
        var provider = ServiceModuleBootstrap.Build(Modules);
        try
        {
            return provider.GetServices<IWorker>().Count() + provider.GetServices<IMetricsSink>().Count();
        }
        finally
        {
            (provider as IDisposable)?.Dispose();
        }
    }
}
