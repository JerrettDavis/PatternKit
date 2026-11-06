# Compensating Transaction Generator

`[GenerateCompensatingTransaction]` emits a `CompensatingTransaction<TContext>` factory from ordered step methods and named compensation methods.

```csharp
[GenerateCompensatingTransaction(TransactionName = "checkout")]
public static partial class CheckoutTransaction
{
    [CompensatingTransactionStep("reserve-inventory", 10, Compensation = nameof(ReleaseInventory))]
    private static ValueTask ReserveInventory(CheckoutContext context, CancellationToken ct) => default;

    private static ValueTask ReleaseInventory(CheckoutContext context, CancellationToken ct) => default;
}
```

Diagnostics:

| ID | Meaning |
| --- | --- |
| `PKCOMP001` | Host type must be partial. |
| `PKCOMP002` | At least one `[CompensatingTransactionStep]` method is required. |
| `PKCOMP003` | Step, compensation, or condition method signature is invalid. |
| `PKCOMP004` | Step names and orders must be unique. |
| `PKCOMP005` | Factory method, transaction name, or compensation configuration is invalid. |
