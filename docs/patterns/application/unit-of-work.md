# Unit of Work

Unit of Work coordinates a set of application operations as one logical commit boundary. PatternKit's `UnitOfWork` runs named steps in order and runs compensating rollback actions in reverse order when a later step fails.

```csharp
var unit = UnitOfWork.Create()
    .Enlist("reserve-inventory", ReserveAsync, ReleaseInventoryAsync)
    .Enlist("capture-payment", CaptureAsync, RefundAsync)
    .Build();

var result = await unit.CommitAsync(ct);
```

Use it around repositories, adapters, and external-resource calls where the application owns the transaction or compensation policy.

See [Unit of Work Generator](../../generators/unit-of-work.md).
