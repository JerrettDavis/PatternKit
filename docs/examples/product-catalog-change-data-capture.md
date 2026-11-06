# Product Catalog Change Data Capture

The product catalog CDC example shows how a catalog service can capture product mutations as ordered integration events and publish them after the mutation is stored.

Import it into a host:

```csharp
services.AddProductCatalogChangeDataCaptureDemo();
```

The demo registers:

- `IChangeDataCaptureStore<ProductMutation,ProductChanged>` for pending capture records.
- `IProductChangePublisher` for event handoff.
- `ChangeDataCapturePipeline<ProductMutation,ProductChanged>` as the application-owned CDC pipeline.
- `ProductCatalogChangeDataCaptureService` as the host-facing workflow.

The source-generated route emits the same pipeline factory while letting the application supply its real publisher and durable store.
