# Fulfillment Competing Consumers Example

The fulfillment competing consumers example demonstrates a production-style work dispatcher with two regional workers competing for orders from the same logical stream.

Files:

- `src/PatternKit.Examples/Messaging/FulfillmentCompetingConsumersExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/FulfillmentCompetingConsumersExampleTests.cs`

The example includes:

- a fluent group factory
- a source-generated group builder
- `IServiceCollection` registration through `AddFulfillmentCompetingConsumersDemo`
- aggregate import through `AddPatternKitExamples`
