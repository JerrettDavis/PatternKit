# Dispatcher Generator

## Overview

The **Dispatcher Generator** creates a standalone Mediator pattern implementation at compile time. It generates a complete message dispatcher with support for commands, notifications, streams, and pipelines—all with zero PatternKit runtime dependencies.

## When to Use

Use the Dispatcher generator when you need to:

- **Decouple components**: Communicate through messages instead of direct calls
- **Implement CQRS**: Separate commands (write) from queries (read)
- **Build event-driven systems**: Fan-out notifications to multiple handlers
- **Add cross-cutting concerns**: Logging, validation, caching via pipelines
- **Stream data**: Async enumerable results for large datasets

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

Add the assembly attribute to generate a dispatcher:

```csharp
using PatternKit.Generators.Messaging;

[assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]
```

This generates:
- `AppDispatcher` — Main dispatcher class
- `AppDispatcher.Builder` — Fluent builder for registration
- `IDispatcherBuilder` — Interface for modular registration
- `IModule` — Interface for handler modules
- `ICommandHandler<TRequest, TResponse>` — Command handler contract
- `INotificationHandler<TNotification>` — Notification handler contract
- `IStreamHandler<TRequest, TItem>` — Stream handler contract

## Message Types

### Commands (Request/Response)

Commands are one-to-one messages that return a response:

```csharp
public record CreateUserCommand(string Name, string Email);
public record CreateUserResponse(int UserId);

var dispatcher = AppDispatcher.Create()
    .Command<CreateUserCommand, CreateUserResponse>(async (cmd, ct) =>
    {
        var user = await userService.CreateAsync(cmd.Name, cmd.Email, ct);
        return new CreateUserResponse(user.Id);
    })
    .Build();

var response = await dispatcher.Send<CreateUserCommand, CreateUserResponse>(
    new CreateUserCommand("Alice", "alice@example.com"));
```

### Notifications (Fan-Out)

Notifications are one-to-many messages with no response:

```csharp
public record UserCreatedNotification(int UserId, string Name);

var dispatcher = AppDispatcher.Create()
    .Notification<UserCreatedNotification>(async (n, ct) =>
    {
        await emailService.SendWelcomeAsync(n.UserId, ct);
    })
    .Notification<UserCreatedNotification>(async (n, ct) =>
    {
        await analyticsService.TrackUserCreatedAsync(n.UserId, ct);
    })
    .Build();

// All handlers are invoked
await dispatcher.Publish(new UserCreatedNotification(123, "Alice"));
```

### Streams (Async Enumerable)

Streams return async sequences:

```csharp
public record GetLogsQuery(DateTime Since);
public record LogEntry(DateTime Timestamp, string Message);

var dispatcher = AppDispatcher.Create()
    .Stream<GetLogsQuery, LogEntry>(async (query, ct) =>
    {
        return logService.GetLogsAsync(query.Since, ct); // IAsyncEnumerable<LogEntry>
    })
    .Build();

await foreach (var log in dispatcher.Stream<GetLogsQuery, LogEntry>(
    new GetLogsQuery(DateTime.UtcNow.AddHours(-1))))
{
    Console.WriteLine($"{log.Timestamp}: {log.Message}");
}
```

## Pipelines

Pipelines add cross-cutting concerns to command handling:

### Pre Hooks

Execute before the handler:

```csharp
.Pre<CreateUserCommand>(async (cmd, ct) =>
{
    _logger.LogInformation("Creating user: {Name}", cmd.Name);
})
```

### Post Hooks

Execute after the handler (with access to response):

```csharp
.Post<CreateUserCommand, CreateUserResponse>(async (cmd, response, ct) =>
{
    _logger.LogInformation("Created user {UserId}", response.UserId);
})
```

### Around Hooks

Wrap the handler (middleware pattern):

```csharp
.Around<CreateUserCommand, CreateUserResponse>(async (cmd, ct, next) =>
{
    var sw = Stopwatch.StartNew();
    try
    {
        return await next();
    }
    finally
    {
        _logger.LogInformation("Command took {Elapsed}ms", sw.ElapsedMilliseconds);
    }
})
```

### OnError Hooks

Execute when an exception occurs:

```csharp
.OnError<CreateUserCommand, CreateUserResponse>(async (cmd, ex, ct) =>
{
    _logger.LogError(ex, "Failed to create user: {Name}", cmd.Name);
})
```

### Pipeline Ordering

Hooks are ordered by the `order` parameter:

```csharp
.Pre<CreateUserCommand>(LoggingHook, order: 0)    // First
.Pre<CreateUserCommand>(ValidationHook, order: 10) // Second
.Around<CreateUserCommand, CreateUserResponse>(TimingHook, order: 0)
```

## Modules

Organize handlers into reusable modules:

```csharp
public class UserModule : IModule
{
    private readonly IUserService _userService;

    public UserModule(IUserService userService) => _userService = userService;

    public void Register(IDispatcherBuilder builder)
    {
        builder
            .Command<CreateUserCommand, CreateUserResponse>(CreateUserAsync)
            .Command<GetUserQuery, UserDto?>(GetUserAsync)
            .Notification<UserCreatedNotification>(OnUserCreatedAsync);
    }

    private async ValueTask<CreateUserResponse> CreateUserAsync(
        CreateUserCommand cmd, CancellationToken ct)
    {
        var user = await _userService.CreateAsync(cmd.Name, cmd.Email, ct);
        return new CreateUserResponse(user.Id);
    }

    // ... other handlers
}

// Register module
var dispatcher = AppDispatcher.Create()
    .AddModule(new UserModule(userService))
    .AddModule(new OrderModule(orderService))
    .Build();
```

## Attributes

### `[GenerateDispatcher]`

Assembly-level attribute for generating a dispatcher.

| Property | Type | Default | Description |
|---|---|---|---|
| `Namespace` | `string?` | `"Generated.Messaging"` | Namespace for generated types |
| `Name` | `string?` | `"AppDispatcher"` | Name of generated dispatcher class |
| `IncludeObjectOverloads` | `bool` | `false` | Generate object-based overloads (uses reflection) |
| `IncludeStreaming` | `bool` | `true` | Generate streaming support |
| `Visibility` | `GeneratedVisibility` | `Public` | Visibility of generated types |

## Generated Interfaces

### ICommandHandler<TRequest, TResponse>

```csharp
public interface ICommandHandler<TRequest, TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken ct);
}
```

### INotificationHandler<TNotification>

```csharp
public interface INotificationHandler<TNotification>
{
    ValueTask Handle(TNotification notification, CancellationToken ct);
}
```

### IStreamHandler<TRequest, TItem>

```csharp
public interface IStreamHandler<TRequest, TItem>
{
    IAsyncEnumerable<TItem> Handle(TRequest request, CancellationToken ct);
}
```

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKD006** | Error | Invalid GenerateDispatcher configuration |

## Best Practices

### 1. Use Records for Messages

```csharp
// ✅ Immutable, value equality, concise
public record CreateUserCommand(string Name, string Email);

// ❌ Mutable class
public class CreateUserCommand { public string Name { get; set; } }
```

### 2. One Handler Per Command

Commands should have exactly one handler:

```csharp
// ✅ Single handler
.Command<CreateUserCommand, CreateUserResponse>(HandleCreateUser)

// ❌ Multiple handlers for same command (throws)
.Command<CreateUserCommand, CreateUserResponse>(Handler1)
.Command<CreateUserCommand, CreateUserResponse>(Handler2) // Error!
```

### 3. Use Notifications for Events

When multiple components need to react:

```csharp
// ✅ Multiple handlers OK for notifications
.Notification<OrderPlacedNotification>(SendConfirmationEmail)
.Notification<OrderPlacedNotification>(UpdateInventory)
.Notification<OrderPlacedNotification>(NotifyAnalytics)
```

### 4. Prefer Generic Send Over Object Overloads

```csharp
// ✅ Type-safe, no reflection
await dispatcher.Send<CreateUserCommand, CreateUserResponse>(cmd);

// ⚠️ Uses reflection (only when IncludeObjectOverloads = true)
await dispatcher.Send(cmd);
```

### 5. Use Pipelines for Cross-Cutting Concerns

```csharp
var dispatcher = AppDispatcher.Create()
    // Global validation
    .Pre<CreateUserCommand>(ValidateCommand)
    // Global timing
    .Around<CreateUserCommand, CreateUserResponse>(TimingMiddleware)
    // Global error logging
    .OnError<CreateUserCommand, CreateUserResponse>(LogError)
    // Handler
    .Command<CreateUserCommand, CreateUserResponse>(HandleCommand)
    .Build();
```

## Examples

### CQRS Pattern

```csharp
// Commands (write)
public record CreateOrderCommand(string CustomerId, List<OrderItem> Items);
public record CreateOrderResponse(string OrderId);

// Queries (read)
public record GetOrderQuery(string OrderId);
public record OrderDto(string OrderId, string Status, List<OrderItem> Items);

var dispatcher = AppDispatcher.Create()
    // Commands
    .Command<CreateOrderCommand, CreateOrderResponse>(async (cmd, ct) =>
    {
        var order = await orderService.CreateAsync(cmd, ct);
        return new CreateOrderResponse(order.Id);
    })
    // Queries
    .Command<GetOrderQuery, OrderDto?>(async (query, ct) =>
    {
        return await orderRepository.GetAsync(query.OrderId, ct);
    })
    .Build();
```

### Event Sourcing Integration

```csharp
public record OrderPlacedEvent(string OrderId, DateTime PlacedAt);

var dispatcher = AppDispatcher.Create()
    .Command<PlaceOrderCommand, PlaceOrderResponse>(async (cmd, ct) =>
    {
        var order = await orderService.PlaceAsync(cmd, ct);

        // Publish domain event
        await dispatcher.Publish(new OrderPlacedEvent(order.Id, DateTime.UtcNow), ct);

        return new PlaceOrderResponse(order.Id);
    })
    .Notification<OrderPlacedEvent>(async (e, ct) =>
    {
        await eventStore.AppendAsync(e, ct);
    })
    .Build();
```

### Streaming Large Datasets

```csharp
public record ExportUsersQuery(string Role);
public record UserExportRow(int Id, string Name, string Email);

var dispatcher = AppDispatcher.Create()
    .Stream<ExportUsersQuery, UserExportRow>((query, ct) =>
    {
        return dbContext.Users
            .Where(u => u.Role == query.Role)
            .Select(u => new UserExportRow(u.Id, u.Name, u.Email))
            .AsAsyncEnumerable();
    })
    .Build();

// Stream to CSV
await using var writer = new StreamWriter("export.csv");
await foreach (var row in dispatcher.Stream<ExportUsersQuery, UserExportRow>(
    new ExportUsersQuery("Admin")))
{
    await writer.WriteLineAsync($"{row.Id},{row.Name},{row.Email}");
}
```

### Validation Pipeline

```csharp
public class ValidationModule : IModule
{
    public void Register(IDispatcherBuilder builder)
    {
        builder.Pre<CreateUserCommand>(async (cmd, ct) =>
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                throw new ValidationException("Name is required");

            if (!IsValidEmail(cmd.Email))
                throw new ValidationException("Invalid email format");
        });
    }

    private static bool IsValidEmail(string email) => email.Contains('@');
}

var dispatcher = AppDispatcher.Create()
    .AddModule(new ValidationModule())
    .Command<CreateUserCommand, CreateUserResponse>(HandleCreateUser)
    .Build();
```

## Troubleshooting

### PKD006: Invalid configuration

**Cause:** GenerateDispatcher attribute has invalid values.

**Fix:** Ensure valid namespace and name:
```csharp
// ✅ Valid
[assembly: GenerateDispatcher(Namespace = "MyApp.Messaging", Name = "AppDispatcher")]

// ❌ Invalid (empty namespace)
[assembly: GenerateDispatcher(Namespace = "", Name = "")]
```

### Handler already registered

**Cause:** Attempting to register multiple handlers for the same command type.

**Fix:** Use notifications for fan-out, or merge handlers:
```csharp
// ❌ Throws at build time
.Command<MyCommand, MyResponse>(Handler1)
.Command<MyCommand, MyResponse>(Handler2)

// ✅ Use notification for multiple handlers
.Notification<MyEvent>(Handler1)
.Notification<MyEvent>(Handler2)
```

### Missing handler

**Cause:** Sending a command with no registered handler.

**Fix:** Register a handler before building:
```csharp
var dispatcher = AppDispatcher.Create()
    .Command<MyCommand, MyResponse>(HandleMyCommand) // ✅ Register handler
    .Build();
```

## See Also

- [Patterns: Mediator](../patterns/behavioral/mediator/index.md)
- [Patterns: Messaging](../patterns/messaging/README.md)
- [Strategy Generator](strategy.md) — For predicate-based dispatch
