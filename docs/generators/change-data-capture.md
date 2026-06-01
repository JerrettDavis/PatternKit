# Change Data Capture Generator

`[GenerateChangeDataCapture]` emits a typed factory for `ChangeDataCapturePipeline<TMutation,TEvent>` from a mapper method.

```csharp
[GenerateChangeDataCapture(
    typeof(ProductMutation),
    typeof(ProductChanged),
    FactoryMethodName = "CreatePipeline",
    MapperMethodName = "Map",
    PipelineName = "product-catalog-cdc")]
public static partial class ProductCatalogCdc
{
    public static ProductChanged Map(ProductMutation mutation, long sequence)
        => new(sequence, mutation.Sku, mutation.Name);
}
```

## Diagnostics

| ID | Severity | Message |
| --- | --- | --- |
| `PKCDC001` | Error | The host type must be partial. |
| `PKCDC002` | Error | Factory and mapper method names must be valid C# identifiers. |
| `PKCDC003` | Error | Mutation and event types are required. |

Register generated pipelines by supplying the application publisher and durable store:

```csharp
services.AddSingleton(sp => ProductCatalogCdc.CreatePipeline(
    (changed, ct) => sp.GetRequiredService<IProductChangePublisher>().PublishAsync(changed, ct),
    sp.GetRequiredService<IChangeDataCaptureStore<ProductMutation, ProductChanged>>()));
```
