# CQRS Dispatcher

The CQRS example shows how an application can keep write models and read models explicit while still using PatternKit through normal .NET dependency injection.

The example has two supported paths:

- a fluent `Mediator` path for small applications or runtime-composed modules
- a source-generated `ProductionDispatcher` path for larger applications that want compile-time dispatcher APIs

## Register

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;

var services = new ServiceCollection()
    .AddCqrsDispatcherExample();

using var provider = services.BuildServiceProvider(validateScopes: true);
var example = provider.GetRequiredService<CqrsDispatcherExample>();
```

Applications that only want the generated CQRS dispatcher services can register that lower-level bolt-on directly:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;

var services = new ServiceCollection()
    .AddSourceGeneratedCqrsServices();
```

`AddSourceGeneratedCqrsServices` uses `TryAdd` for infrastructure defaults, so an application can provide its own logger or repositories before calling the extension.

## Fluent Path

```csharp
var fluent = await example.RunFluentAsync(CancellationToken.None);
```

The fluent path builds a `Mediator` with:

- `CreateCqrsOrder` as the command/write operation
- `GetCqrsOrder` as the query/read operation
- `CqrsOrderCreated` as the domain event notification
- pre/post behaviors for observable pipeline execution

## Source-Generated Path

```csharp
var generated = await example.RunSourceGeneratedAsync(provider, CancellationToken.None);
```

The generated path uses `ProductionDispatcher`, generated from `[GenerateDispatcher]`, with command handlers, query handlers, notification handlers, repositories, and logging registered in `IServiceCollection`.

## Production Shape

The TinyBDD scenarios validate that:

- commands mutate the write side and return created state
- queries read back the command result through the read side
- events fan out through notification handlers
- both fluent and generated paths are importable through `IServiceCollection`
- the example advertises dependency injection and source-generation integration surfaces
