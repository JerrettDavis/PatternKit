# Service Layer Generator

`GenerateServiceLayerOperationAttribute` creates a typed `ServiceLayerOperation<TRequest,TResponse>` factory from a static partial host.

```csharp
[GenerateServiceLayerOperation(typeof(RegisterCustomerRequest), typeof(CustomerRegistrationReceipt), FactoryName = "CreateOperation", OperationName = "register-customer")]
public static partial class GeneratedCustomerServiceLayer
{
    [ServiceLayerRule("email", "Email is required.", 10)]
    private static bool HasEmail(RegisterCustomerRequest request)
        => !string.IsNullOrWhiteSpace(request.Email);

    [ServiceLayerHandler]
    private static ValueTask<CustomerRegistrationReceipt> Handle(RegisterCustomerRequest request, CancellationToken cancellationToken)
        => new(new CustomerRegistrationReceipt(request.CustomerId, request.Email));
}
```

The generated factory is equivalent to:

```csharp
ServiceLayerOperation<RegisterCustomerRequest, CustomerRegistrationReceipt>
    .Create("register-customer")
    .Require("email", "Email is required.", HasEmail)
    .Handle(Handle)
    .Build();
```

Rules are ordered by the `order` argument on `[ServiceLayerRule]`.

Diagnostics:

- `PKSL001`: host type must be partial.
- `PKSL002`: exactly one `[ServiceLayerHandler]` method is required.
- `PKSL003`: handler must be static and return `ValueTask<TResponse>` from `(TRequest, CancellationToken)`.
- `PKSL004`: rule must be static and return `bool` from `TRequest`.
- `PKSL005`: rule order values must be unique.
