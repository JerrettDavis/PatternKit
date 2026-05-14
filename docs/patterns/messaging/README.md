# Messaging Patterns

This section covers messaging and event-driven patterns in PatternKit.

## Message Envelope and Context

`Message<TPayload>`, `MessageHeaders`, and `MessageContext` provide shared in-process metadata for enterprise integration patterns. They carry correlation IDs, causation IDs, idempotency keys, content types, reply addresses, timestamps, cancellation, and execution-scoped items without pretending to be a broker or durable queue.

[Learn More](message-envelope.md)

## Enterprise Message Routing

Content router, recipient list, splitter, and aggregator primitives model common Enterprise Integration Patterns for deterministic in-process workflows.

[Learn More](message-routing.md)

## Routing Slip

Runtime and source-generated routing slip factories execute named message itineraries in order and record progress in message headers.

[Learn More](routing-slip.md)

## Saga / Process Manager

Runtime and source-generated process managers coordinate typed message transitions over explicit saga state.

[Learn More](saga.md)

## Mailbox

Bounded or unbounded in-process inboxes serialize async message handling through a single consumer, with explicit backpressure, error, lifecycle, and diagnostics policies.

[Learn More](mailbox.md)

## Idempotent Receiver, Inbox, and Outbox

Idempotency and handoff helpers compose message handlers with pluggable stores, inbox boundaries, and outbox records without claiming broker durability or exactly-once delivery.

[Learn More](reliability.md)

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
- `src/PatternKit.Examples/Messaging/DispatcherExample.cs`
- `src/PatternKit.Examples/MediatorComprehensiveDemo/ComprehensiveDemo.cs` - DI integration, CQRS, behaviors, real-world domain

### Related Patterns

The Source-Generated Mediator complements other PatternKit patterns:

- **[Message Envelope and Context](message-envelope.md)** - Shared metadata for routers, routing slips, sagas, mailboxes, and idempotent receivers
- **[Enterprise Message Routing](message-routing.md)** - Content-based router, recipient list, splitter, and aggregator primitives
- **[Routing Slip](routing-slip.md)** - Ordered message itineraries with fluent runtime and source-generated factories
- **[Saga / Process Manager](saga.md)** - Typed message transitions over explicit long-running process state
- **[Mailbox](mailbox.md)** - Serialized in-process inbox with bounded backpressure and shutdown behavior
- **[Idempotent Receiver, Inbox, and Outbox](reliability.md)** - Pluggable idempotency and handoff helpers for at-least-once processing
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
