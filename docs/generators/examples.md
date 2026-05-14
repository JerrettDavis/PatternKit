# Generator Examples

Production-flavored samples live in `src/PatternKit.Examples` to show generator output in realistic application shapes: DI module registration, application builders, subsystem facades, service proxies, observers, mementos, state machines, strategies, visitors, template workflows, singletons, adapters, and messaging factories.

See [Source Generator Application Suite](../examples/source-generator-application-suite.md) for the full source/test map.

## 1) ServiceModules with FactoryMethod

`ServiceModules` is a `static partial` class annotated with `[FactoryMethod]`:

```csharp
[FactoryMethod(typeof(string), CreateMethodName = "ConfigureModule")]
public static partial class ServiceModules
{
    [FactoryCase("metrics")]
    public static IServiceCollection AddMetrics(IServiceCollection services) =>
        services.AddSingleton<IMetricsSink, ConsoleMetricsSink>();

    [FactoryCase("caching")]
    public static IServiceCollection AddCaching(IServiceCollection services) =>
        services.AddSingleton<ICacheProvider, MemoryCacheProvider>();

    [FactoryDefault]
    public static IServiceCollection AddDefaults(IServiceCollection services) =>
        services.AddSingleton<IWorker, BackgroundWorker>();
}
```

Generated API (sync):

```csharp
ServiceModules.ConfigureModule("metrics", services);
ServiceModules.TryCreate("workers", out var updated, services);
```

Usage (bootstrap modules from config):

```csharp
var services = new ServiceCollection();
foreach (var module in new[] { "metrics", "caching", "workers" })
{
    ServiceModules.ConfigureModule(module, services);
}
var provider = services.BuildServiceProvider();
```

## 2) ApplicationOrchestrator with FactoryClass

An interface/abstract base marked `[FactoryClass]` emits a concrete factory:

```csharp
[FactoryClass(typeof(string), GenerateEnumKeys = true)]
public interface IOrchestratorStep
{
    ValueTask ExecuteAsync(IServiceProvider services, CancellationToken ct = default);
}

[FactoryClassKey("seed")] public sealed class SeedDataStep : IOrchestratorStep { … }
[FactoryClassKey("warm-cache")] public sealed class WarmCacheStep : IOrchestratorStep { … }
[FactoryClassKey("start-workers")] public sealed class StartWorkersStep : IOrchestratorStep { … }
```

Generated factory (partial) is `OrchestratorStepFactory` with:

```csharp
var factory = new OrchestratorStepFactory();
var step = await factory.CreateAsync("warm-cache");
await step.ExecuteAsync(provider, cancellationToken);
```

`ApplicationOrchestrator` consumes it to run configured steps in order. This pattern works well for task pipelines defined by configuration or tenant-specific input.

## 3) CorporateApplication with Builder

A `[GenerateBuilder]` state-projection builder composes a modular host that plugs into `Host.CreateApplicationBuilder()`:

```csharp
[GenerateBuilder(Model = BuilderModel.StateProjection)]
public static partial class CorporateApplication
{
    public static CorporateAppState Seed() => new(Host.CreateApplicationBuilder(), new(), new(), new(), new());

    [BuilderProjector]
    public static CorporateApp Build(CorporateAppState state)
    {
        foreach (var module in state.Modules) module.Configure(state.Builder, state.Log);
        foreach (var customize in state.Customizations) customize(state.Builder);
        return new CorporateApp(state.Builder.Build(), state.StartupTasks, state.Log);
    }
}

// Fluent, no magic strings
var app = await CorporateApplicationDemo.CreateBuilder()
    .ForEnvironment(CorporateEnvironment.Production)
    .EnableMessaging()
    .EnableJobs()
    .LoadSecrets()
    .AddStartupTasks()
    .BuildAndInitializeAsync();
```

The demo wires observability, messaging, background jobs, async secret loading, and startup tasks before emitting a ready-to-run `CorporateApp` instance.

## Additional Generator Examples

| Generator | Example source | Tests |
| --- | --- | --- |
| Adapter | `src/PatternKit.Examples/AdapterGeneratorDemo` | `test/PatternKit.Examples.Tests/AdapterGeneratorDemo` |
| Facade | `src/PatternKit.Examples/Generators/Facade` | `test/PatternKit.Examples.Tests/Generators/FacadeSpecsTests.cs` |
| Memento | `src/PatternKit.Examples/Generators/Memento` | `test/PatternKit.Examples.Tests/Generators/MementoGeneratorExamplesTests.cs` |
| Observer | `src/PatternKit.Examples/ObserverGeneratorDemo` | `test/PatternKit.Examples.Tests/ObserverGeneratorDemo` |
| Proxy | `src/PatternKit.Examples/ProxyGeneratorDemo` | `test/PatternKit.Examples.Tests/ProxyGeneratorDemo` |
| Singleton | `src/PatternKit.Examples/SingletonGeneratorDemo` | `test/PatternKit.Examples.Tests/SingletonGeneratorDemo` |
| State Machine | `src/PatternKit.Examples/Generators/State` | `test/PatternKit.Examples.Tests/Generators/StateGeneratorExamplesTests.cs` |
| Strategy | `src/PatternKit.Examples/Generators/Strategies` | `test/PatternKit.Examples.Tests/Generators/StrategySpecsTests.cs` |
| Template Method | `src/PatternKit.Examples/TemplateMethodGeneratorDemo` | `test/PatternKit.Examples.Tests/TemplateMethodGeneratorDemo` |
| Visitor | `src/PatternKit.Examples/Generators/Visitors` | `test/PatternKit.Examples.Tests/Generators/VisitorGeneratorExamplesTests.cs` |
| Messaging | `src/PatternKit.Examples/Messaging` | `test/PatternKit.Examples.Tests/Messaging` |

## Where to Find the Code

- `src/PatternKit.Examples/Generators/FactoryGeneratorExamples.cs`
- `src/PatternKit.Examples/Generators/Builders/CorporateApplicationBuilderDemo`
- `src/PatternKit.Examples/Messaging`
- Project references `PatternKit.Generators` as an analyzer so generated APIs are emitted at build time.
