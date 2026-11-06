# Pipes and Filters Generator

`[GeneratePipesAndFiltersPipeline]` emits a typed builder factory. The generated method configures the pipeline name; application code adds filters from normal services before building.

```csharp
[GeneratePipesAndFiltersPipeline(
    typeof(OrderContext),
    FactoryMethodName = "CreatePipeline",
    PipelineName = "fulfillment")]
public static partial class FulfillmentPipeline;

var pipeline = FulfillmentPipeline.CreatePipeline()
    .AddFilter("validate", (ctx, ct) => validator.ApplyAsync(ctx, ct))
    .Build();
```

Diagnostics:

- `PKPF001`: the host type must be `partial`.
