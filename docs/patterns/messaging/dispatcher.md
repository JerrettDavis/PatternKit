# Mediator (Source Generated)

## Overview

The **Mediator pattern** provides a standalone, source-generated mediator for decoupling communication between components. This implementation handles:

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

## What is the Mediator Pattern?

The **Mediator pattern** is a behavioral design pattern that reduces coupling between components by having them communicate through a central mediator object instead of directly with each other. This pattern:

- **Centralizes communication logic**: All message routing happens in one place
- **Reduces dependencies**: Components don't need to know about each other
- **Simplifies maintenance**: Changes to message flow are isolated to the mediator
- **Enables cross-cutting concerns**: Behaviors like logging, validation, and metrics can be applied uniformly

### Source-Generated vs Runtime Mediator

PatternKit offers two Mediator implementations:

1. **Runtime Mediator** ([docs](../behavioral/mediator/index.md)) - A pre-built, allocation-light mediator using `PatternKit.Behavioral.Mediator`
2. **Source-Generated Mediator** (this document) - A compile-time generated mediator with **zero PatternKit runtime dependency**

Choose the source-generated variant when:
- You need **zero runtime dependencies** (for libraries/NuGet packages)
- You want **AOT compatibility** without reflection
- You prefer **compile-time verification** of message flows
- You need **maximum performance** with no abstraction overhead

Choose the runtime mediator when:
- You want **immediate use** without code generation setup
- You need **dynamic handler registration** at runtime
- You're building an application (not a library)

## Production Example with Dependency Injection

For a complete production-ready example, see the comprehensive demo at:
**`src/PatternKit.Examples/MediatorComprehensiveDemo/ComprehensiveDemo.cs`**

This 780+ line example demonstrates:

### DI Integration with IServiceCollection

```csharp
// Setup DI container
var services = new ServiceCollection();

// Register infrastructure
services.AddSingleton<ILogger, InMemoryLogger>();
services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();

// Register mediator and handlers
services.AddSourceGeneratedMediator();
services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly());

// Register pipeline behaviors
services.AddTransient(typeof(LoggingBehavior<,>));
services.AddTransient(typeof(ValidationBehavior<,>));
services.AddTransient(typeof(PerformanceBehavior<,>));

var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<ProductionDispatcher>();
```

### Extension Methods Provided

The comprehensive demo includes these MediatR-style extension methods:

#### AddSourceGeneratedMediator()
Registers the dispatcher and wires up all handlers with DI container:

```csharp
services.AddSourceGeneratedMediator();
```

#### AddHandlersFromAssembly()
Automatically discovers and registers all handlers from an assembly:

```csharp
services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly());
```

This scans for and registers:
- `ICommandHandler<TRequest, TResponse>` implementations
- `INotificationHandler<TNotification>` implementations  
- `IStreamHandler<TRequest, TItem>` implementations

#### AddBehavior<TRequest, TResponse, TBehavior>()
Registers specific pipeline behaviors:

```csharp
services.AddBehavior<CreateCustomerCommand, Customer, LoggingBehavior<CreateCustomerCommand, Customer>>();
```

### Real-World Domain Model

The comprehensive demo implements a complete e-commerce domain:

**Commands (Write Operations)**:
- `CreateCustomerCommand` - Create new customers
- `PlaceOrderCommand` - Place orders with line items
- `ProcessPaymentCommand` - Process payments

**Queries (Read Operations)**:
- `GetCustomerQuery` - Retrieve customer by ID
- `GetOrdersByCustomerQuery` - Get all orders for a customer

**Events/Notifications**:
- `CustomerCreatedEvent` - Fan out to welcome email, audit log, stats update
- `OrderPlacedEvent` - Notify inventory, send confirmation
- `PaymentProcessedEvent` - Record audit trail

**Streams**:
- `SearchProductsQuery` - Async enumerable product search results

### Pipeline Behaviors Demonstrated

**LoggingBehavior**: Logs all commands before and after execution

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request, 
        CancellationToken ct, 
        RequestHandlerDelegate<TResponse> next)
    {
        _logger.Log($"[Logging] Handling {typeof(TRequest).Name}");
        var response = await next();
        _logger.Log($"[Logging] Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

**ValidationBehavior**: Validates commands before execution

**PerformanceBehavior**: Tracks execution time metrics

**TransactionBehavior**: Wraps commands in transactions (skips queries)

### Complete Usage Flow

The demo shows 6 complete end-to-end scenarios:

```csharp
// 1. Create Customer (Command + Event)
var customer = await dispatcher.Send<CreateCustomerCommand, Customer>(
    new CreateCustomerCommand("John Doe", "john@example.com", 5000m), 
    default);

await dispatcher.Publish(
    new CustomerCreatedEvent(customer.Id, customer.Name, customer.Email), 
    default);

// 2. Query Customer
var queriedCustomer = await dispatcher.Send<GetCustomerQuery, Customer?>(
    new GetCustomerQuery(customer.Id), 
    default);

// 3. Place Order (Command + Event)
var order = await dispatcher.Send<PlaceOrderCommand, Order>(
    new PlaceOrderCommand(customer.Id, items), 
    default);

// 4. Process Payment
var success = await dispatcher.Send<ProcessPaymentCommand, bool>(
    new ProcessPaymentCommand(order.Id, order.Total), 
    default);

// 5. Stream Search Results
await foreach (var product in dispatcher.Stream<SearchProductsQuery, ProductSearchResult>(
    new SearchProductsQuery("laptop", 10), 
    default))
{
    Console.WriteLine($"Product: {product.Name} - ${product.Price}");
}

// 6. Query Orders
var orders = await dispatcher.Send<GetOrdersByCustomerQuery, List<Order>>(
    new GetOrdersByCustomerQuery(customer.Id), 
    default);
```

### Repository Pattern Integration

The demo shows how to integrate with repositories:

```csharp
public class GetCustomerHandler : ICommandHandler<GetCustomerQuery, Customer?>
{
    private readonly ICustomerRepository _repository;
    
    public GetCustomerHandler(ICustomerRepository repository)
    {
        _repository = repository;
    }
    
    public ValueTask<Customer?> Handle(GetCustomerQuery request, CancellationToken ct)
    {
        var customer = _repository.GetById(request.CustomerId);
        return new ValueTask<Customer?>(customer);
    }
}
```

### CQRS Pattern

The demo demonstrates Command Query Responsibility Segregation:

- **Commands** modify state: `CreateCustomerCommand`, `PlaceOrderCommand`
- **Queries** read state: `GetCustomerQuery`, `GetOrdersByCustomerQuery`
- Both use the same mediator but have different semantics

### Key Takeaways from the Demo

1. **Full DI Integration**: All components are resolved from the DI container
2. **Handler Discovery**: Automatic registration via assembly scanning
3. **Pipeline Composition**: Multiple behaviors compose around handlers
4. **Event Fan-Out**: Single event triggers multiple handlers
5. **Repository Pattern**: Clean separation between domain and infrastructure
6. **Zero PatternKit Dependency**: Generated code only uses BCL types

## Best Practices

1. **Use value types** for messages when possible (records with value semantics)
2. **Keep handlers focused** - one responsibility per handler
3. **Use pipelines** for cross-cutting concerns (logging, validation, metrics)
4. **Test handlers independently** before composing into dispatcher
5. **Use cancellation tokens** consistently throughout your message flow
6. **Register handlers via DI** - avoid manual instantiation for better testability
7. **Use CQRS** - separate commands (write) from queries (read) for clarity

## Performance

- **Zero allocations** in dispatch path for value types
- **No reflection** - all dispatch is compile-time generated
- **Deterministic** - no runtime scanning or dynamic discovery
- **Minimal overhead** - direct delegate invocation

## Related Patterns

- **[Runtime Mediator](../behavioral/mediator/index.md)** - Pre-built mediator with PatternKit runtime dependency
- **[Observer](../behavioral/observer/index.md)** - For simpler pub/sub scenarios without request/response
- **[Command](../behavioral/command/index.md)** - For encapsulating requests as objects

## Future Enhancements

Future versions may include:
- Object-based overloads (for dynamic scenarios)
- Parallel notification execution
- Around hooks for full pipeline wrapping
- OnError handlers for exception handling
- Module registration for organizing handlers
