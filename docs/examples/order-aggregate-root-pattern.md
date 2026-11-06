# Order Aggregate Root Pattern

This example demonstrates a production-style Aggregate Root for order placement and payment. It includes a fluent command handler, a source-generated command handler, TinyBDD tests, and an `IServiceCollection` extension.

## Import

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.AggregateRootDemo;

var services = new ServiceCollection();
services.AddOrderAggregateRootDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var service = provider.GetRequiredService<OrderAggregateRootService>();
```

## Run

```csharp
var summary = service.Run();
```

The service uses the generated `AggregateCommandHandler<OrderAggregate, OrderCommand, IOrderEvent>`. The fluent route uses the same command decision and event application functions, so teams can compare runtime composition with generated factories without changing domain behavior.

## Production Notes

- Keep command decision pure: inspect aggregate state and return events.
- Keep event application deterministic: event data should be enough to mutate aggregate state.
- Persist and publish `UncommittedEvents` after the unit of work commits, then call `MarkCommitted()`.
