# Generator Examples (DI + Orchestration)

Three production-flavored samples live in `src/PatternKit.Examples/Generators` to show the factory and builder generators in action with `IServiceCollection` and `Host`.

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
[GenerateBuilder(Model = BuilderModel.StateProjection, BuilderTypeName = "CorporateAppBuilder")]
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

// Sample usage
var app = await CorporateApplicationDemo.BuildAsync("Production", "messaging", "jobs");
await app.InitializeAsync();
```

The demo wires observability, messaging, background jobs, async secret loading, and startup tasks before emitting a ready-to-run `CorporateApp` instance.

## Where to find the code

- `src/PatternKit.Examples/Generators/FactoryGeneratorExamples.cs`
- `src/PatternKit.Examples/Generators/CorporateApplicationBuilderDemo.cs`
- Project references `PatternKit.Generators` as an analyzer so the factories/builders are generated at build time.
