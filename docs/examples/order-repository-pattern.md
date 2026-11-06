# Order Repository Pattern

The order repository example models an application persistence boundary that stores orders, queries through a `Specification<OrderRecord>`, rejects duplicate keys, and can be imported through `IServiceCollection`.

```csharp
var services = new ServiceCollection();
services.AddOrderRepositoryDemo();

using var provider = services.BuildServiceProvider();
var workflow = provider.GetRequiredService<OrderRepositoryWorkflow>();
var summary = await workflow.RunAsync();
```

The example includes fluent and source-generated repository factories plus TinyBDD coverage for add/get/query, duplicate handling, and DI import.

Files:

- `src/PatternKit.Examples/RepositoryDemo/OrderRepositoryDemo.cs`
- `test/PatternKit.Examples.Tests/RepositoryDemo/OrderRepositoryDemoTests.cs`
