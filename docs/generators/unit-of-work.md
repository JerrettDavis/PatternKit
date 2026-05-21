# Unit of Work Generator

`[GenerateUnitOfWork]` emits a `UnitOfWork` factory from ordered static step methods.

```csharp
[GenerateUnitOfWork]
public static partial class CheckoutWork
{
    [UnitOfWorkStep("reserve-inventory", 10, RollbackMethodName = nameof(UndoReserve))]
    private static ValueTask Reserve(CancellationToken ct) => default;

    private static ValueTask UndoReserve(CancellationToken ct) => default;
}
```

Diagnostics:

| ID | Meaning |
| --- | --- |
| `PKUOW001` | Host type must be partial. |
| `PKUOW002` | At least one `[UnitOfWorkStep]` method is required. |
| `PKUOW003` | Step methods must return `ValueTask` and accept one `CancellationToken`. |
| `PKUOW004` | Step names and orders must be unique. |
