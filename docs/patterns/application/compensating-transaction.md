# Compensating Transaction

Compensating Transaction records a sequence of reversible business steps. PatternKit's `CompensatingTransaction<TContext>` executes each step in order and, when a later step fails, runs completed compensation actions in reverse order.

```csharp
var transaction = CompensatingTransaction<CheckoutContext>
    .Create("checkout")
    .AddStep("reserve-inventory", ReserveAsync, ReleaseAsync, step => step.At(10))
    .AddStep("authorize-payment", AuthorizeAsync, VoidAsync, step => step.At(20))
    .Build();

var execution = await transaction.ExecuteAsync(context, ct);
```

Use it for workflows that cross boundaries where a database transaction is unavailable: inventory reservations, payment authorization, shipment creation, tenant provisioning, and external API side effects.

See [Compensating Transaction Generator](../../generators/compensating-transaction.md).
