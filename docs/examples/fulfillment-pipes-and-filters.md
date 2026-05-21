# Fulfillment Pipes and Filters Example

The fulfillment pipes and filters example models an order workflow as validation, reservation, and publication filters.

Files:

- `src/PatternKit.Examples/Messaging/FulfillmentPipesAndFiltersExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/FulfillmentPipesAndFiltersExampleTests.cs`

The example includes fluent and source-generated construction, `IServiceCollection` registration through `AddFulfillmentPipesAndFiltersDemo`, and aggregate import through `AddPatternKitExamples`.
