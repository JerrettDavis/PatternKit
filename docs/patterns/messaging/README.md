# Messaging Patterns

This section covers messaging and event-driven patterns in PatternKit.

## Message Dispatcher (Source Generated)

A **zero-dependency**, **source-generated** message dispatcher for commands, notifications, and streams.

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

// Build dispatcher
var dispatcher = AppDispatcher.Create()
    .Command<CreateUser, UserCreated>((req, ct) =>
        new ValueTask<UserCreated>(new UserCreated(1, req.Username)))
    .Build();

// Use dispatcher
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
- [Examples](../../../src/PatternKit.Examples/Messaging/DispatcherExample.cs)

### Related Patterns

The Message Dispatcher complements other PatternKit patterns:

- **Mediator** - For in-memory pub/sub without code generation
- **Command** - For encapsulating requests as objects
- **Observer** - For reactive event handling

### When to Use

Use the source-generated Message Dispatcher when you need:

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
