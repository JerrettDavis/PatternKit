# Comprehensive Production Mediator Demo

This comprehensive example demonstrates how to build a production-ready application using the **source-generated Mediator pattern** with full dependency injection integration, similar to MediatR but with zero runtime dependencies on PatternKit.

## Overview

This 780+ line demo implements a complete e-commerce domain with:

- ✅ **Full DI Integration** with `IServiceCollection`
- ✅ **MediatR-style Extension Methods** for easy setup
- ✅ **CQRS Pattern** (Command Query Responsibility Segregation)
- ✅ **Event-Driven Architecture** with notification fan-out
- ✅ **Async Streaming** with `IAsyncEnumerable<T>`
- ✅ **Pipeline Behaviors** for cross-cutting concerns
- ✅ **Around Middleware** for wrapping handlers
- ✅ **OnError Handling** for exception management
- ✅ **Module System** for modular registration
- ✅ **Object Overloads** for dynamic dispatch (optional)
- ✅ **Repository Pattern** for data access
- ✅ **Real-World Domain** (e-commerce scenario)

## Quick Start

```csharp
// 1. Setup DI container
var services = new ServiceCollection();

// 2. Register infrastructure
services.AddSingleton<ILogger, InMemoryLogger>();
services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();

// 3. Register mediator and handlers
services.AddSourceGeneratedMediator();
services.AddHandlersFromAssembly(Assembly.GetExecutingAssembly());

// 4. Register behaviors
services.AddTransient(typeof(LoggingBehavior<,>));
services.AddTransient(typeof(ValidationBehavior<,>));

// 5. Build and use
var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<ProductionDispatcher>();

// 6. Execute commands
var customer = await dispatcher.Send<CreateCustomerCommand, Customer>(
    new CreateCustomerCommand("John Doe", "john@example.com", 5000m), 
    default);
```

## New Features

### Around Middleware

Wrap handler execution with full control over the pipeline:

```csharp
var dispatcher = AppDispatcher.Create()
    .Command<MyCommand, MyResponse>(handler)
    .Around<MyCommand, MyResponse>(async (req, ct, next) =>
    {
        // Before handler
        Console.WriteLine("Before");
        
        var result = await next();
        
        // After handler
        Console.WriteLine("After");
        
        return result;
    }, order: 1)
    .Build();
```

### OnError Handling

Handle exceptions gracefully with error handlers:

```csharp
var dispatcher = AppDispatcher.Create()
    .Command<FailingCommand, Result>(handler)
    .OnError<FailingCommand, Result>((req, ex, ct) =>
    {
        Console.WriteLine($"Error: {ex.Message}");
        return ValueTask.CompletedTask;
    })
    .Build();
```

### Module System

Organize handlers into reusable modules:

```csharp
public class OrderModule : IModule
{
    public void Register(IDispatcherBuilder builder)
    {
        builder.Command<PlaceOrder, Order>(PlaceOrderHandler);
        builder.Notification<OrderPlaced>(NotifyInventory);
        builder.Notification<OrderPlaced>(SendConfirmation);
    }
}

var dispatcher = AppDispatcher.Create()
    .AddModule(new OrderModule())
    .Build();
```

### Object Overloads

Enable dynamic dispatch for runtime scenarios:

```csharp
// Generate with object overloads enabled
[assembly: GenerateDispatcher(
    Namespace = "MyApp",
    Name = "AppDispatcher",
    IncludeObjectOverloads = true)]

// Use dynamically
object command = new GetCustomer(id);
var result = await dispatcher.Send(command, ct);
```

### Stream Pipelines

Add pipeline hooks for stream requests:

```csharp
var dispatcher = AppDispatcher.Create()
    .Stream<SearchProducts, Product>(SearchHandler)
    .PreStream<SearchProducts>((req, ct) =>
    {
        Console.WriteLine($"Searching for: {req.Query}");
        return ValueTask.CompletedTask;
    })
    .Build();
```

### Pipeline Ordering

Control execution order with explicit ordering:

```csharp
var dispatcher = AppDispatcher.Create()
    .Around<Cmd, Resp>(OuterMiddleware, order: 1)
    .Around<Cmd, Resp>(InnerMiddleware, order: 2)
    .Pre<Cmd>(PreHook, order: 0)
    .Post<Cmd, Resp>(PostHook, order: 0)
    .Build();

// Execution order: Pre(0) -> Around(1) -> Around(2) -> Handler -> Post(0)
```

## Architecture

### Domain Model

**Entities:**
- `Customer` - Customer information with credit limit
- `Order` - Order with line items and totals
- `OrderItem` - Individual items in an order

**Commands (Write Operations):**
- `CreateCustomerCommand` → `Customer`
- `PlaceOrderCommand` → `Order`
- `ProcessPaymentCommand` → `bool`

**Queries (Read Operations):**
- `GetCustomerQuery` → `Customer?`
- `GetOrdersByCustomerQuery` → `List<Order>`

**Events/Notifications:**
- `CustomerCreatedEvent` - Fan out to 2 handlers (welcome email, statistics)
- `OrderPlacedEvent` - Fan out to 2 handlers (inventory, confirmation)
- `PaymentProcessedEvent` - Single handler (audit trail)

**Streams:**
- `SearchProductsQuery` → `IAsyncEnumerable<ProductSearchResult>`

## Pipeline Execution Flow

### Command with Full Pipeline

```
Request → Pre Hooks (ordered) 
       → Around Middleware (outer to inner)
       → Handler
       → Around Middleware (inner to outer)
       → Post Hooks (ordered)
       → Response

On Exception:
       → OnError Hooks (ordered)
       → Exception propagated
```

### Example with Multiple Behaviors

```csharp
dispatcher.Create()
    .Pre<Cmd>(ValidateRequest, order: 0)
    .Around<Cmd, Resp>(LoggingMiddleware, order: 1)
    .Around<Cmd, Resp>(TransactionMiddleware, order: 2)
    .Command<Cmd, Resp>(Handler)
    .Post<Cmd, Resp>(CacheResult, order: 0)
    .OnError<Cmd, Resp>(LogError, order: 0)
    .Build();

// Execution flow:
// 1. Pre: ValidateRequest
// 2. Around(1) Begin: LoggingMiddleware
// 3. Around(2) Begin: TransactionMiddleware
// 4. Handler
// 5. Around(2) End: TransactionMiddleware
// 6. Around(1) End: LoggingMiddleware
// 7. Post: CacheResult
// On error: LogError
```

### Extension Methods

#### `AddSourceGeneratedMediator()`
Registers the dispatcher and wires up all handlers:

```csharp
public static IServiceCollection AddSourceGeneratedMediator(this IServiceCollection services)
```

**What it does:**
- Registers `ProductionDispatcher` as a singleton
- Wires commands to their handlers via DI resolution
- Wires notifications to fan out to all registered handlers
- Wires stream requests to their handlers

#### `AddHandlersFromAssembly()`
Automatically discovers and registers all handlers:

```csharp
public static IServiceCollection AddHandlersFromAssembly(
    this IServiceCollection services, 
    Assembly assembly)
```

**What it discovers:**
- `ICommandHandler<TRequest, TResponse>` implementations
- `INotificationHandler<TNotification>` implementations
- `IStreamHandler<TRequest, TItem>` implementations

**Registration:**
- All handlers are registered as `Transient` (new instance per request)

#### `AddBehavior<TRequest, TResponse, TBehavior>()`
Registers a specific pipeline behavior:

```csharp
public static IServiceCollection AddBehavior<TRequest, TResponse, TBehavior>(
    this IServiceCollection services)
    where TRequest : ICommand<TResponse>
    where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
```

## Pipeline Behaviors

### LoggingBehavior
Logs all commands before and after execution:

```csharp
[Logging] Handling CreateCustomerCommand
[Logging] Handled CreateCustomerCommand
```

### ValidationBehavior
Validates commands before execution (throws if invalid):

```csharp
[Validation] Validating CreateCustomerCommand
```

### PerformanceBehavior
Tracks and logs execution time:

```csharp
[Performance] CreateCustomerCommand executed in 15ms
```

### TransactionBehavior
Wraps commands in transactions (skips queries):

```csharp
[Transaction] Beginning transaction
[Transaction] Committing transaction
```

## Complete Workflow Example

### Scenario: Create Customer and Place Order

```csharp
// 1. Create Customer (Command)
var customer = await dispatcher.Send<CreateCustomerCommand, Customer>(
    new CreateCustomerCommand("John Doe", "john@example.com", 5000m), 
    default);

customerRepo.Add(customer);

// 2. Publish CustomerCreated Event (triggers 2 handlers)
await dispatcher.Publish(
    new CustomerCreatedEvent(customer.Id, customer.Name, customer.Email), 
    default);
// Output:
//   Sending welcome email to john@example.com
//   Updating customer statistics for 1

// 3. Query Customer (Read)
var queriedCustomer = await dispatcher.Send<GetCustomerQuery, Customer?>(
    new GetCustomerQuery(customer.Id), 
    default);

// 4. Place Order (Command)
var order = await dispatcher.Send<PlaceOrderCommand, Order>(
    new PlaceOrderCommand(customer.Id, new List<OrderItem>
    {
        new(1, "Laptop", 1, 999.99m),
        new(2, "Mouse", 2, 29.99m)
    }), 
    default);

orderRepo.Add(order);

// 5. Publish OrderPlaced Event (triggers 2 handlers)
await dispatcher.Publish(
    new OrderPlacedEvent(order.Id, order.CustomerId, order.Total), 
    default);
// Output:
//   Notifying inventory system of order 1000
//   Sending order confirmation for order 1000

// 6. Process Payment (Command)
var paymentSuccess = await dispatcher.Send<ProcessPaymentCommand, bool>(
    new ProcessPaymentCommand(order.Id, order.Total), 
    default);

// 7. Publish PaymentProcessed Event
await dispatcher.Publish(
    new PaymentProcessedEvent(order.Id, order.Total, paymentSuccess), 
    default);
// Output:
//   Recording payment audit: Order=1000, Amount=$1059.97, Success=True

// 8. Stream Product Search
await foreach (var product in dispatcher.Stream<SearchProductsQuery, ProductSearchResult>(
    new SearchProductsQuery("laptop", 3), 
    default))
{
    Console.WriteLine($"Product: {product.Name} - ${product.Price}");
}
// Output:
//   Product: Laptop - $999.99

// 9. Query Orders for Customer
var orders = await dispatcher.Send<GetOrdersByCustomerQuery, List<Order>>(
    new GetOrdersByCustomerQuery(customer.Id), 
    default);

Console.WriteLine($"Customer has {orders.Count} order(s)");
// Output:
//   Customer has 1 order(s)
```

## Handler Implementation Examples

### Command Handler with Repository

```csharp
public class CreateCustomerHandler : ICommandHandler<CreateCustomerCommand, Customer>
{
    private readonly ILogger _logger;
    
    public CreateCustomerHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask<Customer> Handle(CreateCustomerCommand request, CancellationToken ct)
    {
        _logger.Log($"Creating customer: {request.Name}");
        var customer = new Customer(_nextId++, request.Name, request.Email, request.CreditLimit);
        return new ValueTask<Customer>(customer);
    }
}
```

### Query Handler with Repository

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

### Notification Handler (Multiple Allowed)

```csharp
public class SendWelcomeEmailHandler : INotificationHandler<CustomerCreatedEvent>
{
    private readonly ILogger _logger;
    
    public SendWelcomeEmailHandler(ILogger logger)
    {
        _logger = logger;
    }
    
    public ValueTask Handle(CustomerCreatedEvent notification, CancellationToken ct)
    {
        _logger.Log($"Sending welcome email to {notification.Email}");
        return ValueTask.CompletedTask;
    }
}
```

### Stream Handler

```csharp
public class SearchProductsHandler : IStreamHandler<SearchProductsQuery, ProductSearchResult>
{
    private readonly IProductRepository _repository;
    
    public SearchProductsHandler(IProductRepository repository)
    {
        _repository = repository;
    }
    
    public async IAsyncEnumerable<ProductSearchResult> Handle(
        SearchProductsQuery request, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var products = _repository.Search(request.SearchTerm);
        var count = 0;
        
        foreach (var product in products)
        {
            if (count >= request.MaxResults)
                yield break;
                
            await Task.Delay(10, ct); // Simulate async processing
            yield return product;
            count++;
        }
    }
}
```

## CQRS Pattern

The demo demonstrates Command Query Responsibility Segregation:

### Commands (Write Operations)
- Modify system state
- Return modified entity or success indicator
- May trigger side effects (events)
- Examples: `CreateCustomerCommand`, `PlaceOrderCommand`

### Queries (Read Operations)
- Read system state without modification
- No side effects
- Can be cached/optimized differently
- Examples: `GetCustomerQuery`, `GetOrdersByCustomerQuery`

### Benefits
- Clear separation of concerns
- Different optimization strategies for reads vs writes
- Easier to reason about state changes
- Better testability

## Event-Driven Architecture

### Fan-Out Pattern
Single event triggers multiple handlers:

```csharp
await dispatcher.Publish(new CustomerCreatedEvent(...), default);
// Triggers:
//   1. SendWelcomeEmailHandler
//   2. UpdateCustomerStatsHandler
```

### Benefits
- Loose coupling between components
- Easy to add new handlers without modifying existing code
- Handlers execute sequentially (deterministic order)
- Each handler is independent

## Running the Demo

```csharp
await ComprehensiveMediatorDemo.RunAsync();
```

### Expected Output

```
=== Source-Generated Mediator - Comprehensive Production Demo ===

--- Demo 1: Create Customer ---
  Creating customer: John Doe
  Sending welcome email to john@example.com
  Updating customer statistics for 1

--- Demo 2: Query Customer ---
  Found customer: John Doe

--- Demo 3: Place Order ---
  Placing order for customer 1
  Notifying inventory system of order 1000
  Sending order confirmation for order 1000

--- Demo 4: Process Payment ---
  Processing payment for order 1000: $1059.97
  Recording payment audit: Order=1000, Amount=$1059.97, Success=True

--- Demo 5: Stream Product Search ---
  Product: Mouse - $29.99
  Product: Monitor - $299.99
  Product: Headphones - $149.99

--- Demo 6: Query Customer Orders ---
Customer has 1 order(s)

=== Demo Complete ===

Total operations logged: 25
```

## Key Takeaways

1. **Zero Dependencies**: Generated code only uses BCL types (no PatternKit reference needed)
2. **DI First**: All components resolved from container for better testability
3. **Extensible**: Easy to add new handlers, behaviors, and messages
4. **Type Safe**: Compile-time verification of message flows
5. **Performant**: No reflection, deterministic dispatch
6. **Production Ready**: Shows real-world patterns (CQRS, Repository, Events)
7. **MediatR Compatible**: Similar API for easy migration

## Learn More

- [Full Documentation](../../../docs/patterns/messaging/dispatcher.md)
- [Simple Examples](../Messaging/DispatcherExample.cs)
- [PatternKit Documentation](../../../README.md)
