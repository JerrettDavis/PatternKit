# Shipping Bulkhead

This example models a production shipping allocator with bounded capacity. It demonstrates the same bulkhead rule through:

- a fluent `BulkheadPolicy<ShippingAllocation>`
- a source-generated bulkhead policy factory
- an `IServiceCollection` extension that imports the demo into a standard .NET host

```csharp
var services = new ServiceCollection();
services.AddShippingBulkheadDemo();

using var provider = services.BuildServiceProvider();
var shipping = provider.GetRequiredService<ShippingBulkheadService>();

var allocation = await shipping.ReserveAsync("ORDER-100");
```

The registered demo uses the generated policy path and a scripted shipping allocator. Applications can replace `IShippingAllocator` with their own implementation while keeping the same policy registration shape.

The accompanying TinyBDD tests validate the fluent path, the generated path, overflow behavior, and the DI integration.
