using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Factories;

namespace PatternKit.Examples.Generators.Factories;

// Example 1: Use FactoryMethod to map configuration keys to IServiceCollection wiring.
[FactoryMethod(typeof(string), CreateMethodName = "ConfigureModule")]
public static partial class ServiceModules
{
    [FactoryCase("metrics")]
    public static IServiceCollection AddMetrics(IServiceCollection services)
    {
        services.AddSingleton<IMetricsSink, ConsoleMetricsSink>();
        return services;
    }

    [FactoryCase("caching")]
    public static IServiceCollection AddCaching(IServiceCollection services)
    {
        services.AddSingleton<ICacheProvider, MemoryCacheProvider>();
        return services;
    }

    [FactoryCase("workers")]
    public static IServiceCollection AddWorkers(IServiceCollection services)
    {
        services.AddSingleton<IWorker, BackgroundWorker>();
        return services;
    }

    [FactoryDefault]
    public static IServiceCollection AddDefaults(IServiceCollection services)
    {
        services.AddSingleton<IMetricsSink, ConsoleMetricsSink>();
        services.AddSingleton<IWorker, BackgroundWorker>();
        return services;
    }
}

public static class ServiceModuleBootstrap
{
    public static IServiceProvider Build(string[] modules)
    {
        var services = new ServiceCollection();
        foreach (var module in modules)
        {
            ServiceModules.ConfigureModule(module, services);
        }

        return services.BuildServiceProvider();
    }
}

// Example 2: Use FactoryClass to orchestrate application steps from configuration keys.
[FactoryClass(typeof(string), GenerateEnumKeys = true)]
public interface IOrchestratorStep
{
    ValueTask ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken = default);
}

[FactoryClassKey("seed")]
public sealed class SeedDataStep : IOrchestratorStep
{
    public ValueTask ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var seeder = services.GetService<ISeeder>();
        seeder?.Seed();
        return ValueTask.CompletedTask;
    }
}

[FactoryClassKey("warm-cache")]
public sealed class WarmCacheStep : IOrchestratorStep
{
    public async ValueTask ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (services.GetService<ICacheProvider>() is { } cache)
        {
            await cache.PrimeAsync(cancellationToken);
        }
    }
}

[FactoryClassKey("start-workers")]
public sealed class StartWorkersStep : IOrchestratorStep
{
    public ValueTask ExecuteAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (services.GetService<IWorker>() is { } worker)
        {
            worker.Start();
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class ApplicationOrchestrator(IServiceProvider services)
{
    private readonly OrchestratorStepFactory _factory = new();
    private readonly IServiceProvider _services = services;

    public async Task RunAsync(IEnumerable<string> steps, CancellationToken cancellationToken = default)
    {
        foreach (var key in steps)
        {
            var step = _factory.Create(key);
            await step.ExecuteAsync(_services, cancellationToken);
        }
    }
}

// Lightweight sample service abstractions used in the demos.
public interface IMetricsSink
{
    void Write(string name, double value);
}

public sealed class ConsoleMetricsSink : IMetricsSink
{
    public void Write(string name, double value) => Console.WriteLine($"{name}:{value}");
}

public interface ICacheProvider
{
    Task PrimeAsync(CancellationToken cancellationToken);
}

public sealed class MemoryCacheProvider : ICacheProvider
{
    public Task PrimeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public interface ISeeder
{
    void Seed();
}

public interface IWorker
{
    void Start();
}

public sealed class BackgroundWorker : IWorker
{
    public void Start() { }
}
