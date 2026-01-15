# Messaging Patterns

This section covers messaging and event-driven patterns in PatternKit, with a focus on the **Mediator pattern**.

## Mediator (Source Generated)

A **zero-dependency**, **source-generated Mediator pattern** implementation for commands, notifications, and streams.

The **Mediator pattern** reduces coupling between components by centralizing communication through a mediator object. This source-generated variant provides compile-time code generation with zero runtime dependencies on PatternKit.

[Learn More →](dispatcher.md)

### Quick Start

```csharp
using PatternKit.Generators.Messaging;

// Mark assembly for generation
[assembly: GenerateDispatcher(
    Namespace = "MyApp.Messaging",
    Name = "AppDispatcher")]

// Define messages
public record CreateUser(string Username, string Email);
public record UserCreated(int UserId, string Username);

// Build mediator
var dispatcher = AppDispatcher.Create()
    .Command<CreateUser, UserCreated>((req, ct) =>
        new ValueTask<UserCreated>(new UserCreated(1, req.Username)))
    .Build();

// Use mediator
var result = await dispatcher.Send<CreateUser, UserCreated>(
    new CreateUser("alice", "alice@example.com"),
    cancellationToken);
```

### Key Features

- ✅ **Zero runtime dependency** on PatternKit
- ✅ **AOT-friendly** - no reflection
- ✅ **Async-first** - ValueTask & IAsyncEnumerable<T>
- ✅ **Commands** - request → response
- ✅ **Notifications** - fan-out to multiple handlers
- ✅ **Streams** - async enumerable results
- ✅ **Pipelines** - pre/post hooks for cross-cutting concerns
- ✅ **Fluent API** - easy to compose

### Documentation

- [Full Documentation](dispatcher.md)
- [Simple Examples](../../src/PatternKit.Examples/Messaging/DispatcherExample.cs)
- **[Comprehensive Production Demo](../../src/PatternKit.Examples/MediatorComprehensiveDemo/ComprehensiveDemo.cs)** - DI integration, CQRS, behaviors, real-world domain

### Related Patterns

The Source-Generated Mediator complements other PatternKit patterns:

- **[Runtime Mediator](../behavioral/mediator/index.md)** - Pre-built mediator with PatternKit runtime (use for application code)
- **Observer** - For reactive event handling and pub/sub
- **Command** - For encapsulating requests as objects

### When to Use

Use the source-generated Mediator when you need:

- ✅ Decoupled message handling in your application
- ✅ Compile-time verification of message flows
- ✅ Zero runtime dependencies (for libraries/NuGet packages)
- ✅ AOT deployment scenarios
- ✅ High-performance message routing without reflection

### When Not to Use

Consider alternatives when:

- ❌ You need distributed messaging (use message broker instead)
- ❌ You need dynamic handler discovery at runtime
- ❌ Your handlers come from plugins/dynamic assemblies
- ❌ You need complex routing logic (use message broker)
