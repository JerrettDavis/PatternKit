# Order Identity Map Pattern

This example loads an order through a repository-backed workflow and uses an Identity Map to make repeated loads of the same key return the same object instance.

## What It Demonstrates

- fluent `IdentityMap<TEntity,TKey>` creation
- generated identity-map factory with `[GenerateIdentityMap]`
- duplicate key rejection
- request-scoped `IServiceCollection` registration
- repository integration for repeated loads

## Import

```csharp
var services = new ServiceCollection();
services.AddOrderIdentityMapDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderIdentityMapWorkflow>();
var summary = workflow.Run();
```

The registered `IIdentityMap<TrackedOrder,string>` is scoped so each request or unit of work gets its own identity cache.
