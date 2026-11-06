# Service Layer

Service Layer models application-facing operations behind a typed boundary. It centralizes preconditions, orchestration, persistence calls, and result handling so controllers, workers, and hosted services can depend on an operation instead of coordinating the workflow themselves.

PatternKit provides `IServiceOperation<TRequest,TResponse>` and `ServiceLayerOperation<TRequest,TResponse>` in `PatternKit.Application.ServiceLayer`.

```csharp
var operation = ServiceLayerOperation<RegisterCustomerRequest, CustomerRegistrationReceipt>
    .Create("register-customer")
    .Require("email", "Email is required.", request => !string.IsNullOrWhiteSpace(request.Email))
    .Handle(async (request, ct) =>
    {
        await repository.AddAsync(new RegisteredCustomer(request.CustomerId, request.Email, request.Segment), ct);
        return new CustomerRegistrationReceipt(request.CustomerId, request.Email);
    })
    .Build();

var result = await operation.ExecuteAsync(request, cancellationToken);
```

The result distinguishes completed, rejected, and failed executions. That makes the operation usable from ASP.NET Core endpoints, background workers, and command handlers without mixing validation branches and exception-only control flow.

Register `IServiceOperation<TRequest,TResponse>` as scoped when it depends on repositories, database sessions, tenant context, or request-scoped infrastructure. Use the source-generated path when operation rules and handler methods are stable application code.

See also:

- [Service Layer generator](../../generators/service-layer.md)
- [Customer Service Layer example](../../examples/customer-service-layer-pattern.md)
