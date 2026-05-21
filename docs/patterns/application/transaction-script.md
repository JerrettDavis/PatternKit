# Transaction Script

Transaction Script models one application operation as an explicit workflow: validate input, coordinate persistence or integrations, and return a typed result. Use it when the business transaction is procedural and does not need a rich domain object to own the behavior.

PatternKit provides `TransactionScript<TRequest,TResponse>` in `PatternKit.Application.TransactionScript`.

```csharp
var script = TransactionScript<SubmitOrderRequest, SubmitOrderReceipt>
    .Create("submit-order")
    .Validate(request => request.Total <= 0m
        ? [new TransactionScriptError("total", "Order total must be positive.")]
        : [])
    .Execute(async (request, ct) =>
    {
        await repository.AddAsync(new SubmittedOrder(request.OrderId, request.CustomerId, request.Total), ct);
        return new SubmitOrderReceipt(request.OrderId, request.Total);
    })
    .Build();

var result = await script.ExecuteAsync(request, cancellationToken);
```

The runtime path returns `TransactionScriptResult<TResponse>` so callers can distinguish completed, rejected, and failed executions without manual assertions or exception-only control flow.

Use the source-generated path when the script handler and validator are stable application code. Register `ITransactionScript<TRequest,TResponse>` as scoped when the script depends on repositories, unit-of-work state, or request-scoped infrastructure.

See also:

- [Transaction Script generator](../../generators/transaction-script.md)
- [Order Transaction Script example](../../examples/order-transaction-script-pattern.md)
