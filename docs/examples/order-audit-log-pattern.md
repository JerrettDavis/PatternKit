# Order Audit Log Pattern

This production-shaped example shows an order audit trail that records submitted and approved actions.

It demonstrates:

- fluent `InMemoryAuditLog<OrderAuditEntry,string>` construction
- generated audit log factory with `[GenerateAuditLog]`
- append-only order action recording and query behavior
- scoped `IAuditLog<OrderAuditEntry,string>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddOrderAuditLogDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderAuditLogWorkflow>();
var summary = await workflow.SubmitAndApproveAsync("order-100");
```

The registered audit log is scoped so importing applications can compose it with request-scoped transaction, tenant, user, or storage services.

Files:

- `src/PatternKit.Examples/AuditLogDemo/OrderAuditLogDemo.cs`
- `test/PatternKit.Examples.Tests/AuditLogDemo/OrderAuditLogDemoTests.cs`
