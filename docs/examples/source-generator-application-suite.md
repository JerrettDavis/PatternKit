# Source Generator Application Suite

This example suite shows how PatternKit generators remove boilerplate while preserving explicit application architecture. The examples live under `src/PatternKit.Examples/Generators` and are validated by `test/PatternKit.Examples.Tests/Generators`.

The examples are intentionally application-shaped: DI module setup, orchestrated startup steps, generated facades, builder-driven host composition, source-generated strategies, generated mementos, generated state machines, and generated visitors.

## Pattern Map

| Generator | Example source | Test source | Production shape |
| --- | --- | --- | --- |
| Builder | `src/PatternKit.Examples/Generators/Builders/CorporateApplicationBuilderDemo` | `test/PatternKit.Examples.Tests/Generators/CorporateApplicationBuilderDemoTests.cs` | Host/application composition similar to ASP.NET Core extension methods. |
| Factory Method | `src/PatternKit.Examples/Generators/Factories/FactoryGeneratorExamples.cs` | `test/PatternKit.Examples.Tests/Generators/FactoryGeneratorExamplesTests.cs` | Configuration keys map to `IServiceCollection` module wiring. |
| Factory Class | `src/PatternKit.Examples/Generators/Factories/FactoryGeneratorExamples.cs` | `test/PatternKit.Examples.Tests/Generators/FactoryGeneratorExamplesTests.cs` | Startup orchestration steps are selected by stable string keys. |
| Facade | `src/PatternKit.Examples/Generators/Facade` | `test/PatternKit.Examples.Tests/Generators/FacadeSpecsTests.cs` | Billing and shipping subsystem operations are exposed through focused facades. |
| Memento | `src/PatternKit.Examples/Generators/Memento` | `test/PatternKit.Examples.Tests/Generators/MementoGeneratorExamplesTests.cs` | Editor and game state snapshots support undo, restore, and state checkpointing. |
| State Machine | `src/PatternKit.Examples/Generators/State/OrderFlowDemo.cs` | `test/PatternKit.Examples.Tests/Generators/StateGeneratorExamplesTests.cs` | Order lifecycle transitions are generated from explicit state metadata. |
| Strategy | `src/PatternKit.Examples/Generators/Strategies/StrategySpecs.cs` | `test/PatternKit.Examples.Tests/Generators/StrategySpecsTests.cs` | Generated action, result, and try strategies replace repetitive routing scaffolding. |
| Visitor | `src/PatternKit.Examples/Generators/Visitors/DocumentProcessingDemo.cs` | `test/PatternKit.Examples.Tests/Generators/VisitorGeneratorExamplesTests.cs` | Document processing uses generated visitor interfaces, accept methods, and builders. |
| Adapter | `src/PatternKit.Examples/AdapterGeneratorDemo` | `test/PatternKit.Examples.Tests/AdapterGeneratorDemo/AdapterGeneratorDemoTests.cs` | External payment, logging, and clock contracts are adapted into internal abstractions. |
| Observer | `src/PatternKit.Examples/ObserverGeneratorDemo` | `test/PatternKit.Examples.Tests/ObserverGeneratorDemo/ObserverGeneratorDemoTests.cs` | Notification and telemetry systems use generated event hubs and typed subscriptions. |
| Proxy | `src/PatternKit.Examples/ProxyGeneratorDemo` | `test/PatternKit.Examples.Tests/ProxyGeneratorDemo/ProxyGeneratorDemoTests.cs` | Payment service calls are wrapped by generated authentication, caching, logging, retry, and timing interceptors. |
| Singleton | `src/PatternKit.Examples/SingletonGeneratorDemo` | `test/PatternKit.Examples.Tests/SingletonGeneratorDemo/SingletonGeneratorDemoTests.cs` | Configuration, clocks, and service registries expose generated singleton accessors. |
| Template Method | `src/PatternKit.Examples/TemplateMethodGeneratorDemo` | `test/PatternKit.Examples.Tests/TemplateMethodGeneratorDemo` | Import and order-processing workflows are generated from ordered steps and hook points. |
| Messaging | `src/PatternKit.Examples/Messaging` | `test/PatternKit.Examples.Tests/Messaging` | Dispatchers, content routers, routing slips, and sagas are generated for in-process enterprise integration flows. |

## Corporate Application Builder

`CorporateApplicationBuilderDemo` is the most complete host-style generator example. It models a production startup surface where teams compose a host with environment selection, observability, messaging, background jobs, secrets, startup tasks, and module validation.

```csharp
var app = await CorporateApplicationDemo.CreateBuilder()
    .ForEnvironment(CorporateEnvironment.Production)
    .EnableMessaging()
    .EnableJobs()
    .LoadSecrets()
    .AddStartupTasks()
    .RequireModules()
    .BuildAndInitializeAsync();
```

The generated builder keeps fluent application composition explicit. Extension methods such as `EnableMessaging()` and `EnableJobs()` remain handwritten application policy, while `[GenerateBuilder]` owns the repetitive builder plumbing.

## DI Module And Orchestrator Factories

`FactoryGeneratorExamples.cs` demonstrates two factory styles:

- `[FactoryMethod]` maps module names to `IServiceCollection` setup functions.
- `[FactoryClass]` maps orchestration keys to concrete startup steps.

```csharp
var provider = ServiceModuleBootstrap.Build(["metrics", "caching", "workers"]);
var orchestrator = new ApplicationOrchestrator(provider);
await orchestrator.RunAsync(["warm-cache", "start-workers"]);
```

This shape fits systems where module and startup order are configuration-owned, but object creation should still be compile-time generated and testable.

## Generated Cross-Cutting Examples

The generator examples cover common enterprise seams:

- Generated facades simplify billing and shipping subsystem APIs.
- Generated proxies wrap service calls with authentication, caching, retry, logging, and timing.
- Generated observers centralize notification and telemetry dispatch.
- Generated mementos snapshot mutable editor/game state.
- Generated template methods enforce workflow order while preserving hook points.
- Generated state machines keep lifecycle transitions deterministic.

## Messaging Generator Examples

Messaging examples live under `src/PatternKit.Examples/Messaging`:

- `DispatcherExample.cs` demonstrates source-generated mediator commands, notifications, streams, and paging.
- `ContentRouterGeneratorExample.cs` demonstrates generated content-based routing.
- `RoutingSlipExample.cs` demonstrates generated routing-slip factories for fulfillment workflows.
- `SagaExample.cs` demonstrates generated process-manager transitions over explicit state.

See [Messaging Generators](../generators/messaging.md) and [Enterprise Messaging Workflow Suite](enterprise-messaging-workflows.md) for deeper routing and workflow guidance.

## Validation

These examples are compiled with `PatternKit.Generators` as an analyzer reference, so generated code is exercised during normal builds. The examples test project verifies behavior across the supported target frameworks, and DocFX validates the crosslinks in this page.
