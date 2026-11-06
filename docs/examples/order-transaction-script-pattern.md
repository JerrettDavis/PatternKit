# Order Transaction Script Pattern

This production-shaped example shows a submit-order application operation as a Transaction Script.

It demonstrates:

- fluent `TransactionScript<SubmitOrderRequest,SubmitOrderReceipt>` construction
- generated script factory with `[GenerateTransactionScript]`
- repository and unit-of-work coordination inside the script handler
- scoped `ITransactionScript<SubmitOrderRequest,SubmitOrderReceipt>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddOrderTransactionScriptDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<OrderTransactionScriptWorkflow>();
var summary = await workflow.SubmitAsync(new SubmitOrderRequest("order-100", "customer-1", 125m));
```

The registered script is scoped so importing applications can safely compose it with request-scoped repositories, units of work, database sessions, or tenant services.

Files:

- `src/PatternKit.Examples/TransactionScriptDemo/OrderTransactionScriptDemo.cs`
- `test/PatternKit.Examples.Tests/TransactionScriptDemo/OrderTransactionScriptDemoTests.cs`
