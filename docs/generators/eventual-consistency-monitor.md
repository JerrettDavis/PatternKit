# Eventual Consistency Monitor Generator

The Eventual Consistency Monitor generator creates a strongly typed factory for `EventualConsistencyMonitor<TKey>` from a partial host type.

```csharp
using PatternKit.Generators.EventualConsistency;

[GenerateEventualConsistencyMonitor(
    typeof(string),
    FactoryMethodName = "CreateMonitor",
    MonitorName = "order-projection-consistency",
    MaxAllowedLag = 1)]
public static partial class GeneratedOrderProjectionConsistencyMonitor;
```

Generated output:

```csharp
EventualConsistencyMonitor<string> monitor =
    GeneratedOrderProjectionConsistencyMonitor.CreateMonitor();
```

Use the fluent path when runtime configuration owns the lag threshold, comparer, or clock. Use the generated path when a module wants a compile-time construction surface with stable monitor naming and threshold settings.

## Diagnostics

- `PKECM001`: the eventual consistency monitor host type must be partial.
- `PKECM002`: `FactoryMethodName` and `MonitorName` must be non-empty and `MaxAllowedLag` must be non-negative.
