# Eventual Consistency Monitor

Eventual Consistency Monitor tracks source and target watermarks for projections, replicas, and integrations. It surfaces whether a target has converged, is still lagging, or is missing one side of the observation.

`EventualConsistencyMonitor<TKey>` provides the fluent runtime path:

```csharp
var monitor = EventualConsistencyMonitor<string>
    .Create("order-projection-consistency")
    .WithMaxAllowedLag(1)
    .Build();

monitor.RecordSource("ORDER-100", 10);
var evaluation = monitor.RecordTarget("ORDER-100", 9);

if (evaluation.IsConverged)
{
    // projection is within the accepted lag threshold
}
```

Each evaluation includes the monitor name, key, status, source/target watermarks, allowed lag, and current lag.

## Use When

- A projection, replica, or integration needs visible convergence status.
- Operators need to distinguish missing observations from real lag.
- Tests need deterministic source/target watermark evaluation without relying on infrastructure metrics.

## Compare With

- Use Materialized View for the read model itself.
- Use Event Sourcing or Event-Carried State Transfer for facts that move state forward.
- Use Outbox for reliable message publication and Eventual Consistency Monitor for observing convergence after publication.
