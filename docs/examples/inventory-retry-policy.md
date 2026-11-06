# Inventory Retry Policy

This example models a production inventory lookup that can receive a transient service response before stock data becomes available. It demonstrates the same retry rule through:

- a fluent `RetryPolicy<InventoryResponse>`
- a source-generated retry policy factory
- an `IServiceCollection` extension that imports the demo into a standard .NET host

```csharp
var services = new ServiceCollection();
services.AddInventoryRetryDemo();

using var provider = services.BuildServiceProvider();
var lookup = provider.GetRequiredService<InventoryLookupService>();

var result = await lookup.CheckAsync("SKU-42");
```

The registered demo uses the generated policy path and a scripted inventory client. Applications can replace `IInventoryClient` with their own implementation while keeping the same policy registration shape.

The accompanying TinyBDD tests validate the fluent path, the generated path, and the DI integration.
