# Scheduler Agent Supervisor Generator

`[GenerateSchedulerAgentSupervisor]` creates a typed `SchedulerAgentSupervisor<TWork, TResult>` factory from agent methods and an optional retry predicate.

```csharp
[GenerateSchedulerAgentSupervisor(typeof(WarehouseReplenishmentWork), typeof(WarehouseReplenishmentSummary), SupervisorName = "warehouse-replenishment-scheduler")]
public static partial class GeneratedWarehouseScheduler
{
    [SchedulerAgent("release-replenishment")]
    private static WarehouseReplenishmentSummary Release(SchedulerAgentContext<WarehouseReplenishmentWork> context)
        => new(context.Work.BatchId, context.Attempt);

    [SchedulerRetryWhen]
    private static bool Retry(Exception exception, SchedulerAgentContext<WarehouseReplenishmentWork> context)
        => exception is InvalidOperationException;
}
```

Generated factories configure `MaxAttempts`, `RetryDelayMilliseconds`, all declared agents, and the retry predicate. Agent methods must be static and return the configured result type from `SchedulerAgentContext<TWork>`.
