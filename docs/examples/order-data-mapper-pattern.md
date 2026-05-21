# Order Data Mapper Pattern

This example maps an `OrderAggregate` domain model to an `OrderRow` persistence record and back again, then stores the row through the Repository pattern.

## What It Demonstrates

- fluent `DataMapper<OrderAggregate,OrderRow>` construction
- generated mapper factory with `[GenerateDataMapper]`
- validation errors for invalid domain/data inputs
- repository integration after mapping
- `IServiceCollection` import through `AddOrderDataMapperDemo()`

## Import

```csharp
var services = new ServiceCollection();
services.AddOrderDataMapperDemo();

using var provider = services.BuildServiceProvider(validateScopes: true);
var workflow = provider.GetRequiredService<OrderDataMapperWorkflow>();
var summary = await workflow.RunAsync();
```

The workflow uses `IDataMapper<OrderAggregate,OrderRow>` so applications can replace the mapper with the generated factory, an audited fluent mapper, or a test double.

## Source

- `src/PatternKit.Examples/DataMapperDemo/OrderDataMapperDemo.cs`
- `test/PatternKit.Examples.Tests/DataMapperDemo/OrderDataMapperDemoTests.cs`
