# Transaction Script Generator

`GenerateTransactionScriptAttribute` creates a typed `TransactionScript<TRequest,TResponse>` factory from a static partial host.

```csharp
[GenerateTransactionScript(typeof(SubmitOrderRequest), typeof(SubmitOrderReceipt), FactoryName = "CreateScript", ScriptName = "submit-order")]
public static partial class GeneratedSubmitOrderScript
{
    [TransactionScriptValidator]
    private static IEnumerable<TransactionScriptError> Validate(SubmitOrderRequest request)
        => request.Total <= 0m
            ? [new TransactionScriptError("total", "Order total must be positive.")]
            : [];

    [TransactionScriptHandler]
    private static ValueTask<SubmitOrderReceipt> Handle(SubmitOrderRequest request, CancellationToken cancellationToken)
        => new(new SubmitOrderReceipt(request.OrderId, request.Total));
}
```

The generated factory is equivalent to:

```csharp
TransactionScript<SubmitOrderRequest, SubmitOrderReceipt>
    .Create("submit-order")
    .Validate(Validate)
    .Execute(Handle)
    .Build();
```

Diagnostics:

- `PKTS001`: host type must be partial.
- `PKTS002`: exactly one `[TransactionScriptHandler]` method is required.
- `PKTS003`: handler must be static and return `ValueTask<TResponse>` from `(TRequest, CancellationToken)`.
- `PKTS004`: validator must be a single static method returning `IEnumerable<TransactionScriptError>` from `TRequest`.
