# Order Projection Eventual Consistency Monitor

This example models an order projection monitor that compares the event stream source watermark with the projection target watermark. It reports lagging projections until the target catches up within the configured threshold.

The fluent path builds the monitor directly:

```csharp
var monitor = OrderProjectionConsistencyPolicies.CreateFluentMonitor();
```

The generated path uses a source-generated factory:

```csharp
var monitor = GeneratedOrderProjectionConsistencyMonitor.CreateMonitor();
```

The example is importable through standard dependency injection:

```csharp
services.AddOrderProjectionConsistencyDemo();
```

`OrderProjectionConsistencyService` records source and target progress for an order projection. Production applications can feed it from event-store positions, outbox delivery offsets, replica checkpoints, or projection table versions.
