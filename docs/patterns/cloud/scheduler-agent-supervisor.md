# Scheduler Agent Supervisor

Scheduler Agent Supervisor coordinates due work, dispatches it to an agent, and applies supervision rules for retries and exhaustion.

```csharp
var scheduler = SchedulerAgentSupervisor<WarehouseReplenishmentWork, WarehouseReplenishmentSummary>
    .Create("warehouse-replenishment-scheduler")
    .Supervision(SchedulerSupervisionPolicy<WarehouseReplenishmentWork>.Create()
        .MaxAttempts(2)
        .RetryDelay(TimeSpan.FromSeconds(5))
        .Build())
    .Agent("release-replenishment", ctx => new(ctx.Work.BatchId, ctx.Attempt))
    .Build();

scheduler.Schedule("replenish:B-100", work, dueAt);
var results = scheduler.RunDue(now);
```

Use it for scheduled background jobs where the application needs deterministic result capture, retry policy, and a clear supervision boundary. Production integrations should back scheduling state with durable storage and import the supervisor through `IServiceCollection`.

The source-generated path uses `[GenerateSchedulerAgentSupervisor]`, `[SchedulerAgent]`, and `[SchedulerRetryWhen]`. Import the example through `AddWarehouseSchedulerAgentSupervisorDemo()` or `AddPatternKitExamples()`.
