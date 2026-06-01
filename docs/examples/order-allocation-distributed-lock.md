# Order Allocation Distributed Lock

The order allocation example protects inventory allocation with a resource lease so competing workers cannot mutate the same order concurrently.

```csharp
services.AddOrderAllocationDistributedLockDemo();

var runner = provider.GetRequiredService<OrderAllocationDistributedLockDemoRunner>();
var summary = runner.RunGenerated();
```

The example includes fluent and source-generated construction, a container-owned workflow, and an `IServiceCollection` extension that can be imported into a standard .NET host.
