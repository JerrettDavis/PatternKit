using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Generators.Factories;

namespace PatternKit.Examples.Tests.GeneratorTests;

public sealed class ServiceModulesTests
{
    [Fact]
    public void ConfigureModule_Metrics_Adds_MetricsSink()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("metrics", services);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IMetricsSink>());
    }

    [Fact]
    public void ConfigureModule_Caching_Adds_CacheProvider()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("caching", services);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ICacheProvider>());
    }

    [Fact]
    public void ConfigureModule_Workers_Adds_Worker()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("workers", services);
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IWorker>());
    }

    [Fact]
    public void ConfigureModule_Unknown_Uses_Defaults()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("unknown-module", services);
        var provider = services.BuildServiceProvider();

        // Defaults add MetricsSink and Worker
        Assert.NotNull(provider.GetService<IMetricsSink>());
        Assert.NotNull(provider.GetService<IWorker>());
    }
}

public sealed class ServiceModuleBootstrapTests
{
    [Fact]
    public void Build_With_Metrics_Module()
    {
        var provider = ServiceModuleBootstrap.Build(["metrics"]);

        Assert.NotNull(provider.GetService<IMetricsSink>());
    }

    [Fact]
    public void Build_With_Multiple_Modules()
    {
        var provider = ServiceModuleBootstrap.Build(["metrics", "caching", "workers"]);

        Assert.NotNull(provider.GetService<IMetricsSink>());
        Assert.NotNull(provider.GetService<ICacheProvider>());
        Assert.NotNull(provider.GetService<IWorker>());
    }

    [Fact]
    public void Build_With_Empty_Modules()
    {
        var provider = ServiceModuleBootstrap.Build([]);

        Assert.NotNull(provider);
    }
}

public sealed class ConsoleMetricsSinkTests
{
    [Fact]
    public void Write_Does_Not_Throw()
    {
        var sink = new ConsoleMetricsSink();

        sink.Write("test_metric", 123.45);
    }
}

public sealed class MemoryCacheProviderTests
{
    [Fact]
    public async Task PrimeAsync_Completes_Successfully()
    {
        var cache = new MemoryCacheProvider();

        await cache.PrimeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PrimeAsync_With_Cancellation()
    {
        var cache = new MemoryCacheProvider();
        using var cts = new CancellationTokenSource();

        await cache.PrimeAsync(cts.Token);
    }
}

public sealed class BackgroundWorkerTests
{
    [Fact]
    public void Start_Does_Not_Throw()
    {
        var worker = new BackgroundWorker();

        worker.Start();
    }
}

public sealed class OrchestratorStepFactoryTests
{
    [Fact]
    public void CreateFromKey_Seed_Returns_SeedDataStep()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step = factory.CreateFromKey("seed");

        Assert.IsType<SeedDataStep>(step);
    }

    [Fact]
    public void CreateFromKey_WarmCache_Returns_WarmCacheStep()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step = factory.CreateFromKey("warm-cache");

        Assert.IsType<WarmCacheStep>(step);
    }

    [Fact]
    public void CreateFromKey_StartWorkers_Returns_StartWorkersStep()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step = factory.CreateFromKey("start-workers");

        Assert.IsType<StartWorkersStep>(step);
    }

    [Fact]
    public void CreateFromKey_CaseInsensitive()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step1 = factory.CreateFromKey("SEED");
        var step2 = factory.CreateFromKey("Seed");
        var step3 = factory.CreateFromKey("seed");

        Assert.IsType<SeedDataStep>(step1);
        Assert.IsType<SeedDataStep>(step2);
        Assert.IsType<SeedDataStep>(step3);
    }

    [Fact]
    public void CreateFromKey_UnknownKey_Throws()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        Assert.Throws<KeyNotFoundException>(() => factory.CreateFromKey("unknown"));
    }

    [Fact]
    public void Constructor_Null_Services_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OrchestratorStepFactory(null!));
    }

    [Fact]
    public void CreateFromKey_Null_Key_Throws()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        Assert.Throws<ArgumentNullException>(() => factory.CreateFromKey(null!));
    }
}

public sealed class SeedDataStepTests
{
    [Fact]
    public async Task ExecuteAsync_Without_Seeder()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var step = new SeedDataStep();

        await step.ExecuteAsync(services);
    }

    [Fact]
    public async Task ExecuteAsync_With_Seeder()
    {
        var seeder = new TestSeeder();
        var services = new ServiceCollection()
            .AddSingleton<ISeeder>(seeder)
            .BuildServiceProvider();
        var step = new SeedDataStep();

        await step.ExecuteAsync(services);

        Assert.True(seeder.WasSeeded);
    }

    private sealed class TestSeeder : ISeeder
    {
        public bool WasSeeded { get; private set; }
        public void Seed() => WasSeeded = true;
    }
}

public sealed class WarmCacheStepTests
{
    [Fact]
    public async Task ExecuteAsync_Without_CacheProvider()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var step = new WarmCacheStep();

        await step.ExecuteAsync(services);
    }

    [Fact]
    public async Task ExecuteAsync_With_CacheProvider()
    {
        var services = new ServiceCollection()
            .AddSingleton<ICacheProvider, MemoryCacheProvider>()
            .BuildServiceProvider();
        var step = new WarmCacheStep();

        await step.ExecuteAsync(services);
    }
}

public sealed class StartWorkersStepTests
{
    [Fact]
    public async Task ExecuteAsync_Without_Worker()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var step = new StartWorkersStep();

        await step.ExecuteAsync(services);
    }

    [Fact]
    public async Task ExecuteAsync_With_Worker()
    {
        var worker = new TestWorker();
        var services = new ServiceCollection()
            .AddSingleton<IWorker>(worker)
            .BuildServiceProvider();
        var step = new StartWorkersStep();

        await step.ExecuteAsync(services);

        Assert.True(worker.WasStarted);
    }

    private sealed class TestWorker : IWorker
    {
        public bool WasStarted { get; private set; }
        public void Start() => WasStarted = true;
    }
}

// NOTE: ApplicationOrchestrator tests are skipped because the code relies on
// source-generated OrchestratorStepFactory which is only available when the
// PatternKit.Generators source generator runs. The stub implementation throws
// when no services are provided.

