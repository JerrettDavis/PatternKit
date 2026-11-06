# Change Data Capture

Change Data Capture tracks durable data mutations as ordered integration events. Use it when application state changes must be published to downstream consumers without coupling the write path to a broker, webhook, or projection updater.

## Fluent Path

```csharp
var pipeline = ChangeDataCapturePipeline<ProductMutation, ProductChanged>
    .Create("product-catalog-cdc")
    .UseStore(store)
    .MapWith((mutation, sequence) => new ProductChanged(sequence, mutation.Sku, mutation.Name))
    .PublishWith((changed, ct) => publisher.PublishAsync(changed, ct))
    .Build();

await pipeline.CaptureAsync(new ProductMutation("sku-1", "Desk"));
await pipeline.PublishPendingAsync();
```

`CaptureAsync` appends an ordered CDC entry before publication. `PublishPendingAsync` reads unpublished entries in sequence, publishes each event through the configured publisher, marks successes as published, and leaves failures pending with an incremented attempt count.

## Production Notes

`InMemoryChangeDataCaptureStore<TMutation,TEvent>` is deterministic for tests and examples. Production hosts should implement `IChangeDataCaptureStore<TMutation,TEvent>` over the same durable store that commits the business mutation so the change record and domain write share a transaction boundary.

See [Product Catalog Change Data Capture](../../examples/product-catalog-change-data-capture.md).
