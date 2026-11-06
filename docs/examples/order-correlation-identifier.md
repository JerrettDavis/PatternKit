# Order Correlation Identifier Example

`OrderCorrelationIdentifierExample` demonstrates request/reply correlation for an order workflow.

The example includes:

- a fluent `OrderCorrelationIdentifiers.Create()` factory for `CorrelationIdentifier<CorrelatedOrder>`;
- a generated `GeneratedOrderCorrelation.Create()` factory for a custom `X-Correlation` header;
- `AddOrderCorrelationIdentifierDemo()` for `IServiceCollection` and generic host integration;
- TinyBDD coverage for fluent, generated, and DI usage.

Import it into an application with:

```csharp
services.AddOrderCorrelationIdentifierDemo();
```

Resolve `OrderCorrelationService` or `OrderCorrelationIdentifierExampleRunner` to run the production-shaped sample.
