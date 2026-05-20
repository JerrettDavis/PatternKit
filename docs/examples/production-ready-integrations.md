# Production-Ready Example Integrations

PatternKit examples are meant to be importable building blocks, not throwaway snippets. The examples package now exposes a production-readiness catalog that applications, tests, documentation tooling, and ASP.NET Core diagnostics can use to discover which examples exist, where their source/tests/docs live, and which integration surfaces they exercise.

## Register the catalog

Use the standard .NET dependency injection container:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;

var services = new ServiceCollection()
    .AddLogging()
    .AddPatternKitExampleCatalog();

using var provider = services.BuildServiceProvider(validateScopes: true);
var catalog = provider.GetRequiredService<IPatternKitExampleCatalog>();
```

## Register runnable examples

The examples package also exposes a fluent IoC surface for every catalog entry. Existing applications can import the complete example suite with one call:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;

var services = new ServiceCollection()
    .AddLogging()
    .AddPatternKitExamples();

using var provider = services.BuildServiceProvider(validateScopes: true);

var pricing = provider.GetRequiredService<PricingCalculatorExample>();
var catalog = provider.GetRequiredService<IPatternKitExampleCatalog>();
```

Each example also has its own focused extension, so sample applications can import only the slice they need:

```csharp
services
    .AddPricingCalculatorExample()
    .AddPaymentProcessorDecoratorExample()
    .AddMessagingBackplaneFacadeExample();
```

Every extension registers a concrete typed entry point, such as `PricingCalculatorExample`, `EnterpriseFeatureSlicesExample`, `MinimalWebRequestRouterExample`, or `ReactiveTransactionExample`. The companion `PatternKitExampleServiceDescriptor` registrations make the IoC surface auditable: tests compare those descriptors against the production-readiness catalog and resolve every registered example through `IServiceProvider`.

Each `PatternKitExampleDescriptor` includes:

| Field | Purpose |
| --- | --- |
| `Name` | Human-readable example name. |
| `SourcePath` | Repository source file or entry point. |
| `TestPath` | TinyBDD scenario coverage for the example. |
| `DocumentationPath` | DocFX page for the example. |
| `Integration` | Tooling surfaces covered: DI, options, generic host, ASP.NET Core, source generators, messaging, or external infrastructure. |
| `Patterns` | PatternKit primitives demonstrated by the example. |
| `ProductionChecks` | The behaviors that make the example production-shaped and regression-testable. |

## Validate in a generic host

Applications can fail fast during startup when catalog metadata is malformed or when source/docs/tests are missing from a repository checkout:

```csharp
using Microsoft.Extensions.Hosting;
using PatternKit.Examples.ProductionReadiness;

var builder = Host.CreateApplicationBuilder(args);

builder.AddPatternKitExampleHostedValidation(options =>
{
    options.RepositoryRoot = "/work/PatternKit";
    options.FailOnInvalid = true;
});

using var host = builder.Build();
await host.StartAsync();
```

When `RepositoryRoot` is unset, validation still checks descriptor quality: names, paths, integration surfaces, listed patterns, and production checks. Supplying a repository root also verifies that the referenced source, test, and documentation files exist.

## Expose diagnostics in ASP.NET Core

ASP.NET Core apps can map a tiny diagnostics endpoint for internal documentation portals or build verification:

```csharp
using PatternKit.Examples.ProductionReadiness;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPatternKitExampleCatalog();

var app = builder.Build();
app.MapPatternKitExampleCatalog();

await app.RunAsync();
```

This maps:

| Route | Result |
| --- | --- |
| `/patternkit/examples` | The catalog descriptors. |
| `/patternkit/examples/validation` | A validation report, with HTTP 500 when validation fails. |

## Readiness Contract

Every documented example should satisfy the same baseline contract:

| Requirement | How it is enforced |
| --- | --- |
| Documented in DocFX | `docs/examples/toc.yml` links to the page and documentation tests validate hrefs. |
| Covered by TinyBDD scenarios | The catalog points to a test file and catalog tests verify it exists. |
| Importable from the examples package | The catalog is part of `PatternKit.Examples` and is registered through `IServiceCollection`/`IHostApplicationBuilder`. |
| Has an IoC entry point | `AddPatternKitExamples()` and per-example `Add...Example()` extensions register typed example services through `IServiceCollection`. |
| Host/tooling friendly where applicable | Integration flags identify examples that demonstrate `IOptions`, DI, generic host, ASP.NET Core, source generators, messaging, or external infrastructure. |
| Production-shaped behavior called out | `ProductionChecks` records the behaviors each example must keep validating. |

## Current Catalog Scope

The catalog covers every page in the examples ToC, including:

| Area | Examples |
| --- | --- |
| Request and pipeline composition | Auth/logging chain, minimal web router, mediated transaction pipeline, configuration-driven pipeline. |
| Host and DI composition | Enterprise feature slices, source-generator application suite, messaging backplane facade. |
| Messaging | Enterprise messaging workflow suite, resilient checkout, collaborating mailboxes, message router visitor. |
| POS and pricing | Payment processor decorator, pricing calculator, POS app state, tender visitor. |
| Behavioral workflows | Async state machine, reactive ViewModel, reactive transaction, template method sync/async, event processing visitor. |
| Structural and creational patterns | Flyweight glyph cache, proxy demos, prototype registry, text editor memento, observer hub. |

The catalog is intentionally small and static. It is easy to audit in code review, cheap to resolve at runtime, and suitable for use in build checks, docs portals, sample applications, or internal architecture dashboards.
