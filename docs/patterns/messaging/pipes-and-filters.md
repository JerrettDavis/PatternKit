# Pipes and Filters

Pipes and Filters decomposes a message-processing workflow into ordered filters. Each filter receives the current context and returns the context for the next filter.

```csharp
var pipeline = PipesAndFiltersPipeline<OrderContext>
    .Create("fulfillment")
    .AddFilter("validate", (ctx, ct) => ValidateAsync(ctx, ct))
    .AddFilter("reserve", (ctx, ct) => ReserveAsync(ctx, ct))
    .Build();

var result = await pipeline.ExecuteAsync(context);
```

The runtime validates names, required filters, and delegates, and returns filter execution metadata with the final value.
