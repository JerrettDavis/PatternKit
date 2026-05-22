# Scatter-Gather

Scatter-Gather sends a request to multiple recipients and aggregates the replies into one decision.

```csharp
var quotes = ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary>
    .Create("supplier-quotes")
    .AddRecipient("regional", RegionalQuote)
    .AddRecipient("cold-chain", ColdChainQuote, (message, context) => message.Payload.RequiresColdChain)
    .AggregateWith(SupplierQuoteScatterGathers.Aggregate)
    .Build();
```

Use it when an application needs to query several providers, services, or workers and then choose the best response. The fluent runtime path supports recipient predicates, named replies, and typed aggregation.

The source-generated path uses `[GenerateScatterGather]`, `[ScatterGatherRecipient]`, and `[ScatterGatherAggregator]`. Import the supplier quote example through `AddSupplierQuoteScatterGatherDemo()` or `AddPatternKitExamples()`.
