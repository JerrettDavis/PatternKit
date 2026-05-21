# Customer Service Layer Pattern

This production-shaped example shows customer registration as a Service Layer operation.

It demonstrates:

- fluent `ServiceLayerOperation<RegisterCustomerRequest,CustomerRegistrationReceipt>` construction
- generated operation factory with `[GenerateServiceLayerOperation]`
- repository coordination inside the operation handler
- scoped `IServiceOperation<RegisterCustomerRequest,CustomerRegistrationReceipt>` registration through `IServiceCollection`

```csharp
var services = new ServiceCollection();
services.AddCustomerServiceLayerDemo();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var workflow = scope.ServiceProvider.GetRequiredService<CustomerServiceLayerWorkflow>();
var summary = await workflow.RegisterAsync(new RegisterCustomerRequest("customer-100", "buyer@example.com", "retail"));
```

The registered operation is scoped so importing applications can compose it with request-scoped repositories, database sessions, tenant services, and ASP.NET Core request services.

Files:

- `src/PatternKit.Examples/ServiceLayerDemo/CustomerServiceLayerDemo.cs`
- `test/PatternKit.Examples.Tests/ServiceLayerDemo/CustomerServiceLayerDemoTests.cs`
