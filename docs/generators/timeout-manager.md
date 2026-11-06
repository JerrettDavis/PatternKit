# Timeout Manager Generator

The Timeout Manager generator creates a strongly typed factory for `TimeoutManager<TKey>` from a partial host type.

```csharp
using PatternKit.Generators.Timeouts;

[GenerateTimeoutManager(typeof(Guid), FactoryMethodName = "CreateGenerated", ManagerName = "order-reservation-timeouts")]
public static partial class GeneratedOrderReservationTimeoutManager;
```

Generated output:

```csharp
TimeoutManager<Guid> manager = GeneratedOrderReservationTimeoutManager.CreateGenerated();
```

The generated path removes repeated factory boilerplate while keeping the runtime manager explicit and importable through normal `IServiceCollection` registration.

## Diagnostics

- `PKTM001`: the host type must be partial.
- `PKTM002`: `FactoryMethodName` and `ManagerName` must be non-empty.
