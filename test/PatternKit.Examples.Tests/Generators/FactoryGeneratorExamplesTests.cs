using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Generators.Factories;
using TinyBDD;

namespace PatternKit.Examples.Tests.GeneratorTests;

public sealed class ServiceModulesTests
{
    [Scenario("ConfigureModule Metrics Adds MetricsSink")]
    [Fact]
    public void ConfigureModule_Metrics_Adds_MetricsSink()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("metrics", services);
        var provider = services.BuildServiceProvider();

        ScenarioExpect.NotNull(provider.GetService<IMetricsSink>());
    }

    [Scenario("ConfigureModule Caching Adds CacheProvider")]
    [Fact]
    public void ConfigureModule_Caching_Adds_CacheProvider()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("caching", services);
        var provider = services.BuildServiceProvider();

        ScenarioExpect.NotNull(provider.GetService<ICacheProvider>());
    }

    [Scenario("ConfigureModule Workers Adds Worker")]
    [Fact]
    public void ConfigureModule_Workers_Adds_Worker()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("workers", services);
        var provider = services.BuildServiceProvider();

        ScenarioExpect.NotNull(provider.GetService<IWorker>());
    }

    [Scenario("ConfigureModule Unknown Uses Defaults")]
    [Fact]
    public void ConfigureModule_Unknown_Uses_Defaults()
    {
        var services = new ServiceCollection();

        ServiceModules.ConfigureModule("unknown-module", services);
        var provider = services.BuildServiceProvider();

        // Defaults add MetricsSink and Worker
        ScenarioExpect.NotNull(provider.GetService<IMetricsSink>());
        ScenarioExpect.NotNull(provider.GetService<IWorker>());
    }
}

public sealed class ServiceModuleBootstrapTests
{
    [Scenario("Build With Metrics Module")]
    [Fact]
    public void Build_With_Metrics_Module()
    {
        var provider = ServiceModuleBootstrap.Build(["metrics"]);

        ScenarioExpect.NotNull(provider.GetService<IMetricsSink>());
    }

    [Scenario("Build With Multiple Modules")]
    [Fact]
    public void Build_With_Multiple_Modules()
    {
        var provider = ServiceModuleBootstrap.Build(["metrics", "caching", "workers"]);

        ScenarioExpect.NotNull(provider.GetService<IMetricsSink>());
        ScenarioExpect.NotNull(provider.GetService<ICacheProvider>());
        ScenarioExpect.NotNull(provider.GetService<IWorker>());
    }

    [Scenario("Build With Empty Modules")]
    [Fact]
    public void Build_With_Empty_Modules()
    {
        var provider = ServiceModuleBootstrap.Build([]);

        ScenarioExpect.NotNull(provider);
    }
}

public sealed class ConsoleMetricsSinkTests
{
    [Scenario("Write Does Not Throw")]
    [Fact]
    public void Write_Does_Not_Throw()
    {
        var sink = new ConsoleMetricsSink();

        sink.Write("test_metric", 123.45);
    }
}

public sealed class MemoryCacheProviderTests
{
    [Scenario("PrimeAsync Completes Successfully")]
    [Fact]
    public async Task PrimeAsync_Completes_Successfully()
    {
        var cache = new MemoryCacheProvider();

        await cache.PrimeAsync(CancellationToken.None);
    }

    [Scenario("PrimeAsync With Cancellation")]
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
    [Scenario("Start Does Not Throw")]
    [Fact]
    public void Start_Does_Not_Throw()
    {
        var worker = new BackgroundWorker();

        worker.Start();
    }
}

public sealed class OrchestratorStepFactoryTests
{
    [Scenario("CreateFromKey Seed Returns SeedDataStep")]
    [Fact]
    public void CreateFromKey_Seed_Returns_SeedDataStep()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step = factory.CreateFromKey("seed");

        ScenarioExpect.IsType<SeedDataStep>(step);
    }

    [Scenario("CreateFromKey WarmCache Returns WarmCacheStep")]
    [Fact]
    public void CreateFromKey_WarmCache_Returns_WarmCacheStep()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step = factory.CreateFromKey("warm-cache");

        ScenarioExpect.IsType<WarmCacheStep>(step);
    }

    [Scenario("CreateFromKey StartWorkers Returns StartWorkersStep")]
    [Fact]
    public void CreateFromKey_StartWorkers_Returns_StartWorkersStep()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step = factory.CreateFromKey("start-workers");

        ScenarioExpect.IsType<StartWorkersStep>(step);
    }

    [Scenario("CreateFromKey CaseInsensitive")]
    [Fact]
    public void CreateFromKey_CaseInsensitive()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        var step1 = factory.CreateFromKey("SEED");
        var step2 = factory.CreateFromKey("Seed");
        var step3 = factory.CreateFromKey("seed");

        ScenarioExpect.IsType<SeedDataStep>(step1);
        ScenarioExpect.IsType<SeedDataStep>(step2);
        ScenarioExpect.IsType<SeedDataStep>(step3);
    }

    [Scenario("CreateFromKey UnknownKey Throws")]
    [Fact]
    public void CreateFromKey_UnknownKey_Throws()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        ScenarioExpect.Throws<KeyNotFoundException>(() => factory.CreateFromKey("unknown"));
    }

    [Scenario("Constructor Null Services Throws")]
    [Fact]
    public void Constructor_Null_Services_Throws()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => new OrchestratorStepFactory(null!));
    }

    [Scenario("CreateFromKey Null Key Throws")]
    [Fact]
    public void CreateFromKey_Null_Key_Throws()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var factory = new OrchestratorStepFactory(services);

        ScenarioExpect.Throws<ArgumentNullException>(() => factory.CreateFromKey(null!));
    }

    [Scenario("ApplicationOrchestrator RunsConfiguredStepsAgainstServices")]
    [Fact]
    public async Task ApplicationOrchestrator_RunsConfiguredStepsAgainstServices()
    {
        var seeder = new TestSeeder();
        var worker = new TestWorker();
        var services = new ServiceCollection()
            .AddSingleton<ISeeder>(seeder)
            .AddSingleton<ICacheProvider, MemoryCacheProvider>()
            .AddSingleton<IWorker>(worker)
            .BuildServiceProvider();
        var orchestrator = new ApplicationOrchestrator(services);

        await orchestrator.RunAsync(["seed", "warm-cache", "start-workers"]);

        ScenarioExpect.True(seeder.WasSeeded);
        ScenarioExpect.True(worker.WasStarted);
    }

    private sealed class TestSeeder : ISeeder
    {
        public bool WasSeeded { get; private set; }
        public void Seed() => WasSeeded = true;
    }

    private sealed class TestWorker : IWorker
    {
        public bool WasStarted { get; private set; }
        public void Start() => WasStarted = true;
    }
}

public sealed class SeedDataStepTests
{
    [Scenario("ExecuteAsync Without Seeder")]
    [Fact]
    public async Task ExecuteAsync_Without_Seeder()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var step = new SeedDataStep();

        await step.ExecuteAsync(services);
    }

    [Scenario("ExecuteAsync With Seeder")]
    [Fact]
    public async Task ExecuteAsync_With_Seeder()
    {
        var seeder = new TestSeeder();
        var services = new ServiceCollection()
            .AddSingleton<ISeeder>(seeder)
            .BuildServiceProvider();
        var step = new SeedDataStep();

        await step.ExecuteAsync(services);

        ScenarioExpect.True(seeder.WasSeeded);
    }

    private sealed class TestSeeder : ISeeder
    {
        public bool WasSeeded { get; private set; }
        public void Seed() => WasSeeded = true;
    }
}

public sealed class WarmCacheStepTests
{
    [Scenario("ExecuteAsync Without CacheProvider")]
    [Fact]
    public async Task ExecuteAsync_Without_CacheProvider()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var step = new WarmCacheStep();

        await step.ExecuteAsync(services);
    }

    [Scenario("ExecuteAsync With CacheProvider")]
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
    [Scenario("ExecuteAsync Without Worker")]
    [Fact]
    public async Task ExecuteAsync_Without_Worker()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var step = new StartWorkersStep();

        await step.ExecuteAsync(services);
    }

    [Scenario("ExecuteAsync With Worker")]
    [Fact]
    public async Task ExecuteAsync_With_Worker()
    {
        var worker = new TestWorker();
        var services = new ServiceCollection()
            .AddSingleton<IWorker>(worker)
            .BuildServiceProvider();
        var step = new StartWorkersStep();

        await step.ExecuteAsync(services);

        ScenarioExpect.True(worker.WasStarted);
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

