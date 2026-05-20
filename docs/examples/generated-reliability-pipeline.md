# Generated Reliability Pipeline

The generated reliability pipeline example shows the fluent reliability primitives and the source-generated path side by side. Both paths process the same duplicate `AcceptOrder` command and dispatch exactly one `ReliabilityOrderAccepted` outbox message.

## Integration Shape

Register the example with the standard .NET container:

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;

var services = new ServiceCollection()
    .AddGeneratedReliabilityPipelineExample();

using var provider = services.BuildServiceProvider(validateScopes: true);
var example = provider.GetRequiredService<GeneratedReliabilityPipelineExample>();
var dispatched = await example.Runner.RunGeneratedAsync();
```

The registered `PatternKitExampleServiceDescriptor` advertises messaging, source generation, and dependency-injection support so host applications can audit what they import.

## Generated Contract

`GeneratedReliabilityOrderPipeline` uses `[GenerateReliabilityPipeline]` to emit:

- `CreateOrderReceiver(IIdempotencyStore)` for the idempotent receiver.
- `CreateInbox(IIdempotencyStore)` for the inbox boundary.
- `CreateOutbox()` for the outbox record store.

The generated receiver is configured with `DuplicatePolicy = "ReplayCompleted"`, so a duplicate message with the same idempotency key replays the completed result instead of invoking the handler again.

## Production Notes

`InMemoryIdempotencyStore` and `InMemoryOutbox<T>` are deterministic for tests, demos, and single-process tools. Production applications should implement `IIdempotencyStore` and outbox persistence over durable storage, usually in the same transaction as the business state change.

## Source

- `src/PatternKit.Examples/Messaging/ReliabilityExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/ReliabilityExampleTests.cs`
- `src/PatternKit.Generators/Messaging/ReliabilityPipelineGenerator.cs`
- `test/PatternKit.Generators.Tests/ReliabilityPipelineGeneratorTests.cs`
