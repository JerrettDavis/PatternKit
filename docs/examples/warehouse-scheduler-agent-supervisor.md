# Warehouse Scheduler Agent Supervisor

The warehouse scheduler demo shows scheduled replenishment work imported through Microsoft dependency injection and Generic Host.

```csharp
services.AddWarehouseSchedulerAgentSupervisorDemo();

var runner = provider.GetRequiredService<WarehouseSchedulerDemoRunner>();
var results = runner.RunGenerated();
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, retry supervision, result capture, and a hosted service that schedules and dispatches work during host startup.
