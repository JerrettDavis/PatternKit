# Warehouse Leader Election

The warehouse leader election example coordinates a single active replenishment worker in a Generic Host application.

```csharp
services.AddWarehouseLeaderElectionDemo();

var runner = provider.GetRequiredService<WarehouseLeaderElectionDemoRunner>();
var log = runner.RunGenerated();
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, and a hosted service that acquires leadership on start and releases it on stop.
