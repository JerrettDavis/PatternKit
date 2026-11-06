# Scatter-Gather Generator

`[GenerateScatterGather]` creates a typed `ScatterGather<TRequest,TResponse,TResult>` factory.

```csharp
[GenerateScatterGather(typeof(SupplierQuoteRequest), typeof(SupplierQuote), typeof(SupplierQuoteSummary))]
public static partial class SupplierQuotes
{
    [ScatterGatherRecipient("regional", 10)]
    private static ScatterGatherReply<SupplierQuote> Regional(Message<SupplierQuoteRequest> message, MessageContext context)
        => ScatterGatherReply<SupplierQuote>.Success(new SupplierQuote("regional", 9.75m, true));

    [ScatterGatherAggregator]
    private static SupplierQuoteSummary Aggregate(
        IReadOnlyList<ScatterGatherReply<SupplierQuote>> replies,
        Message<SupplierQuoteRequest> message,
        MessageContext context)
        => SupplierQuoteScatterGathers.Aggregate(replies, message, context);
}
```

Diagnostics:

- `PKSCG001`: host type must be partial.
- `PKSCG002`: at least one recipient is required.
- `PKSCG003`: recipient signature is invalid.
- `PKSCG004`: aggregator signature is invalid.
- `PKSCG005`: recipient names and orders must be unique.
