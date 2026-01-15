# Message Dispatcher (Source Generated)

## Overview

The Message Dispatcher pattern provides a standalone, source-generated message dispatcher for handling:

- **Commands** (request → response)
- **Notifications** (fan-out to multiple handlers)
- **Streams** (request → async stream of items)
- **Pipelines** (pre/around/post hooks)

### Critical Features

- **Zero runtime dependency** on PatternKit - the generated code is fully independent
- **AOT-friendly** - no reflection-based dispatch
- **Async-first** - all operations use `ValueTask` and `IAsyncEnumerable<T>`
- **Dual-mode API** - supports both class-based and fluent registration

## Basic Usage

### 1. Mark your assembly for code generation

```csharp
using PatternKit.Generators.Messaging;

[assembly: GenerateDispatcher(
    Namespace = "MyApp.Messaging",
    Name = "AppDispatcher",
    IncludeStreaming = true)]
```

### 2. Define your messages

```csharp
// Commands
public record CreateUser(string Username, string Email);
public record UserCreated(int UserId, string Username);

// Notifications
public record UserRegistered(int UserId, string Username, string Email);

// Stream requests
public record SearchQuery(string Term, int MaxResults);
public record SearchResult(string Title, string Url);
```

### 3. Register handlers using the fluent API

```csharp
var dispatcher = AppDispatcher.Create()
    // Command handler
    .Command<CreateUser, UserCreated>((req, ct) =>
        new ValueTask<UserCreated>(new UserCreated(1, req.Username)))
    
    // Notification handlers (multiple allowed)
    .Notification<UserRegistered>((n, ct) => 
    {
        Console.WriteLine($"User {n.Username} registered");
        return ValueTask.CompletedTask;
    })
    
    // Stream handler
    .Stream<SearchQuery, SearchResult>(SearchAsync)
    
    .Build();
```

### 4. Use the dispatcher

```csharp
// Send a command
var result = await dispatcher.Send<CreateUser, UserCreated>(
    new CreateUser("alice", "alice@example.com"),
    cancellationToken);

// Publish a notification
await dispatcher.Publish(
    new UserRegistered(1, "alice", "alice@example.com"),
    cancellationToken);

// Stream results
await foreach (var result in dispatcher.Stream<SearchQuery, SearchResult>(
    new SearchQuery("pattern", 10),
    cancellationToken))
{
    Console.WriteLine(result.Title);
}
```

## Pipelines

Add cross-cutting concerns with pipelines:

```csharp
var dispatcher = AppDispatcher.Create()
    // Pre-execution hook
    .Pre<CreateUser>((req, ct) =>
    {
        Console.WriteLine($"Validating: {req.Username}");
        return ValueTask.CompletedTask;
    })
    
    // Command handler
    .Command<CreateUser, UserCreated>((req, ct) =>
        new ValueTask<UserCreated>(new UserCreated(1, req.Username)))
    
    // Post-execution hook
    .Post<CreateUser, UserCreated>((req, res, ct) =>
    {
        Console.WriteLine($"Created user: {res.UserId}");
        return ValueTask.CompletedTask;
    })
    
    .Build();
```

Pipeline execution order:
1. Pre hooks (in registration order)
2. Command handler
3. Post hooks (in registration order)

## Generated Code Structure

The generator creates three files:

1. **`AppDispatcher.g.cs`** - Main dispatcher with `Send`, `Publish`, and `Stream` methods
2. **`AppDispatcher.Builder.g.cs`** - Fluent builder for registration
3. **`AppDispatcher.Contracts.g.cs`** - Handler interfaces and pipeline delegates

All generated code is **independent of PatternKit** and contains only BCL dependencies.

## Configuration Options

```csharp
[assembly: GenerateDispatcher(
    Namespace = "MyApp.Messaging",          // Target namespace
    Name = "AppDispatcher",                 // Class name
    IncludeStreaming = true,                // Enable streaming support
    IncludeObjectOverloads = false,         // Add object-based overloads
    Visibility = GeneratedVisibility.Public // Public or Internal
)]
```

## Behavior

### Commands
- Exactly one handler per request type (runtime exception if missing)
- Async-first with `ValueTask<TResponse>`
- Pipeline support (Pre/Post)

### Notifications
- Zero or more handlers per notification type
- Zero handlers = no-op (does not throw)
- Sequential execution (deterministic order)

### Streams
- Exactly one handler per request type (runtime exception if missing)
- Lazy enumeration (pull-based)
- Cancellation flows through enumeration

## Examples

See `PatternKit.Examples/Messaging/DispatcherExample.cs` for complete working examples.

## Best Practices

1. **Use value types** for messages when possible (records with value semantics)
2. **Keep handlers focused** - one responsibility per handler
3. **Use pipelines** for cross-cutting concerns (logging, validation, metrics)
4. **Test handlers independently** before composing into dispatcher
5. **Use cancellation tokens** consistently throughout your message flow

## Performance

- **Zero allocations** in dispatch path for value types
- **No reflection** - all dispatch is compile-time generated
- **Deterministic** - no runtime scanning or dynamic discovery
- **Minimal overhead** - direct delegate invocation
