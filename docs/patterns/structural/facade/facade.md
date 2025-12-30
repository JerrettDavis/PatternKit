# Facade Pattern

> **Fluent unified interface to complex subsystem operations**

## Overview

The **Facade** pattern provides a simplified, unified interface to a complex subsystem or set of interfaces. PatternKit's implementation offers a fluent, allocation-light way to coordinate multiple subsystem calls behind named operations:

- **Named operations** that map to complex subsystem interactions
- **Default fallback** for unknown operations
- **Case-insensitive** operation matching support
- **Thread-safe** and reusable after building

Facades decouple clients from subsystem complexity, making your APIs cleaner and easier to use while hiding coordination logic behind simple operation names.

## Mental Model

Think of a facade as a **hotel concierge**:
- Clients make **simple requests** ("book dinner reservation")
- The facade **coordinates** multiple subsystems (restaurant service, transportation, confirmation)
- Clients don't need to know **implementation details** of each subsystem

```
┌─────────────────────────────────────────┐
│                Client                   │
│     facade.Execute("process", order)    │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│               Facade                    │
│          Operation: "process"           │
│  ┌───────────────────────────────────┐  │
│  │ 1. inventoryService.Reserve()     │  │
│  │ 2. paymentService.Charge()        │  │
│  │ 3. shippingService.Schedule()     │  │
│  │ 4. notificationService.Send()     │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

## Quick Start

### Basic Facade

```csharp
using PatternKit.Structural.Facade;

// Simple calculator facade
var calc = Facade<(int a, int b), int>.Create()
    .Operation("add", (in (int a, int b) input) => input.a + input.b)
    .Operation("multiply", (in (int a, int b) input) => input.a * input.b)
    .Operation("subtract", (in (int a, int b) input) => input.a - input.b)
    .Build();

var sum = calc.Execute("add", (5, 3));        // 8
var product = calc.Execute("multiply", (5, 3)); // 15
```

### Coordinating Multiple Subsystems

```csharp
// E-commerce order facade
public class OrderFacade
{
    private readonly InventoryService _inventory;
    private readonly PaymentService _payment;
    private readonly ShippingService _shipping;
    private readonly NotificationService _notification;
    
    public Facade<OrderRequest, OrderResult> BuildFacade()
    {
        return Facade<OrderRequest, OrderResult>.Create()
            .Operation("process", ProcessOrder)
            .Operation("cancel", CancelOrder)
            .Operation("refund", RefundOrder)
            .Default((in OrderRequest req) => 
                new OrderResult { Status = "Unknown operation" })
            .Build();
    }
    
    private OrderResult ProcessOrder(in OrderRequest req)
    {
        // Coordinate multiple subsystems
        var inventoryReserved = _inventory.Reserve(req.Items);
        var paymentTx = _payment.Charge(req.PaymentMethod, req.Total);
        var shipmentId = _shipping.Schedule(req.Address, req.Items);
        _notification.SendConfirmation(req.CustomerId, shipmentId);
        
        return new OrderResult
        {
            Status = "Processed",
            TransactionId = paymentTx,
            ShipmentId = shipmentId
        };
    }
    
    private OrderResult CancelOrder(in OrderRequest req)
    {
        _inventory.Release(req.OrderId);
        _payment.Void(req.OrderId);
        _shipping.Cancel(req.OrderId);
        _notification.SendCancellation(req.CustomerId);
        
        return new OrderResult { Status = "Cancelled" };
    }
    
    private OrderResult RefundOrder(in OrderRequest req)
    {
        _shipping.InitiateReturn(req.OrderId);
        _payment.Refund(req.OrderId);
        _inventory.Restock(req.Items);
        _notification.SendRefundConfirmation(req.CustomerId);
        
        return new OrderResult { Status = "Refunded" };
    }
}

// Usage
var facade = new OrderFacade().BuildFacade();
var result = facade.Execute("process", orderRequest);
```

### Default Fallback

```csharp
// Facade with default operation for unknown commands
var api = Facade<string, string>.Create()
    .Operation("status", (in string _) => "System operational")
    .Operation("version", (in string _) => "v2.1.0")
    .Default((in string cmd) => $"Unknown command: {cmd}")
    .Build();

var status = api.Execute("status", "");    // "System operational"
var unknown = api.Execute("help", "");     // "Unknown command: help"
```

### Case-Insensitive Operations

```csharp
// Case-insensitive operation matching
var greetingFacade = Facade<string, string>.Create()
    .OperationIgnoreCase("Hello", (in string name) => $"Hello, {name}!")
    .OperationIgnoreCase("Goodbye", (in string name) => $"Goodbye, {name}!")
    .Build();

var greet1 = greetingFacade.Execute("hello", "Alice");   // "Hello, Alice!"
var greet2 = greetingFacade.Execute("HELLO", "Bob");     // "Hello, Bob!"
var greet3 = greetingFacade.Execute("HeLLo", "Carol");   // "Hello, Carol!"
```

## API Reference

### Creating a Facade

```csharp
public static Builder Create()
```

Creates a new builder for constructing a facade.

### Registering Operations

```csharp
public Builder Operation(string name, Operation handler)
```

Registers a named operation. Operation names are **case-sensitive**.

**Parameters:**
- `name`: Unique operation name
- `handler`: Delegate `TOut Operation(in TIn input)` that coordinates subsystems

**Throws:**
- `ArgumentException` if an operation with the same name already exists

```csharp
public Builder OperationIgnoreCase(string name, Operation handler)
```

Registers a named operation with **case-insensitive** matching.

⚠️ **Note**: Cannot mix case-sensitive and case-insensitive operations in the same facade.

### Default Operation

```csharp
public Builder Default(Operation handler)
```

Configures a default operation to invoke when the requested operation is not found.

**Example:**
```csharp
var facade = Facade<int, string>.Create()
    .Operation("known", (in int x) => $"Result: {x}")
    .Default((in int x) => "Operation not found")
    .Build();
```

### Building the Facade

```csharp
public Facade<TIn, TOut> Build()
```

Builds an immutable, thread-safe facade.

**Throws:**
- `InvalidOperationException` if no operations and no default are configured

### Executing Operations

```csharp
public TOut Execute(string operationName, in TIn input)
```

Executes the named operation with the given input.

**Throws:**
- `InvalidOperationException` if operation not found and no default configured

**Example:**
```csharp
var result = facade.Execute("process", orderData);
```

```csharp
public bool TryExecute(string operationName, in TIn input, out TOut output)
```

Attempts to execute the named operation. Returns `false` if not found and no default exists.

**Returns:** `true` if operation executed; `false` otherwise

**Example:**
```csharp
if (facade.TryExecute("process", orderData, out var result))
{
    Console.WriteLine($"Success: {result}");
}
else
{
    Console.WriteLine("Operation not found");
}
```

### Checking Operations

```csharp
public bool HasOperation(string operationName)
```

Checks whether an operation with the given name is registered.

**Example:**
```csharp
if (facade.HasOperation("process"))
{
    var result = facade.Execute("process", data);
}
```

## Use Cases

### 1. Simplifying Complex APIs

```csharp
// Before: Client must understand multiple services
var user = userService.GetUser(userId);
var permissions = permissionService.GetPermissions(user);
var roles = roleService.GetRoles(user);
var settings = settingsService.GetSettings(user);

// After: Simple facade operation
var userContext = userFacade.Execute("loadContext", userId);
```

### 2. Legacy System Integration

```csharp
// Hide legacy system complexity behind modern facade
var legacyFacade = Facade<LegacyRequest, ModernResponse>.Create()
    .Operation("migrate", (in LegacyRequest req) => 
    {
        // Complex legacy calls hidden here
        var data = legacySystem.GetData(req.Id);
        var transformed = transformer.Convert(data);
        var validated = validator.Validate(transformed);
        return new ModernResponse(validated);
    })
    .Build();
```

### 3. Microservices Coordination

```csharp
// Coordinate multiple microservice calls
var checkoutFacade = Facade<CheckoutRequest, CheckoutResult>.Create()
    .Operation("checkout", (in CheckoutRequest req) => 
    {
        var cartItems = cartService.GetItems(req.CartId);
        var prices = pricingService.Calculate(cartItems);
        var taxInfo = taxService.CalculateTax(prices, req.ShippingAddress);
        var payment = paymentService.Process(req.PaymentInfo, prices.Total + taxInfo.TaxAmount);
        var order = orderService.Create(cartItems, payment);
        
        return new CheckoutResult 
        { 
            OrderId = order.Id, 
            Total = prices.Total + taxInfo.TaxAmount 
        };
    })
    .Build();
```

### 4. Command Pattern Alternative

```csharp
// Use facade as a lightweight command dispatcher
var commandFacade = Facade<Command, CommandResult>.Create()
    .Operation("create-user", CreateUserHandler)
    .Operation("delete-user", DeleteUserHandler)
    .Operation("update-user", UpdateUserHandler)
    .Default((in Command cmd) => new CommandResult { Success = false, Error = "Unknown command" })
    .Build();

var result = commandFacade.Execute("create-user", command);
```

## Performance Characteristics

- **Allocation-light**: Uses arrays internally, minimal allocations after build
- **O(1) operation lookup**: Dictionary-based operation resolution
- **Thread-safe**: Immutable after building, safe for concurrent use
- **Zero boxing**: Uses `in` parameters for readonly references

## Best Practices

### ✅ Do

- Use facades to **simplify complex subsystems**
- Group **related operations** in a single facade
- Make operations **stateless** (capture services in closures if needed)
- Use **descriptive operation names**
- Provide **default operations** for better error handling

### ❌ Don't

- Don't use facades for **simple 1-to-1 mappings** (use direct calls)
- Don't **mix unrelated operations** in one facade
- Don't make operations **stateful** (facades should be reusable)
- Don't use as a **god object** (keep facades focused)

## Comparison with Other Patterns

| Pattern | Purpose | When to Use |
|---------|---------|-------------|
| **Facade** | Simplify complex subsystem | Multiple services need coordination |
| **Adapter** | Convert one interface to another | Interface incompatibility |
| **Decorator** | Add behavior to objects | Enhance existing behavior |
| **Proxy** | Control access to objects | Lazy loading, access control |

## Advanced Examples

### Async Coordination (using Task)

```csharp
// Facade coordinating async operations
var asyncFacade = Facade<string, Task<string>>.Create()
    .Operation("fetch", async (in string url) =>
    {
        var http = new HttpClient();
        var cache = new CacheService();
        
        // Check cache first
        if (cache.TryGet(url, out var cached))
            return cached;
        
        // Fetch and cache
        var content = await http.GetStringAsync(url);
        await cache.SetAsync(url, content);
        return content;
    })
    .Build();

var content = await facade.Execute("fetch", "https://api.example.com/data");
```

### Error Handling

```csharp
var robustFacade = Facade<Request, Result>.Create()
    .Operation("process", (in Request req) =>
    {
        try
        {
            var step1 = service1.Process(req);
            var step2 = service2.Process(step1);
            var step3 = service3.Process(step2);
            return new Result { Success = true, Data = step3 };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Process failed");
            return new Result { Success = false, Error = ex.Message };
        }
    })
    .Build();
```

## See Also

- [Adapter Pattern](../adapter/fluent-adapter.md) - For interface conversion
- [Decorator Pattern](../decorator/index.md) - For behavior enhancement
- [Strategy Pattern](../../behavioral/strategy/strategy.md) - For algorithm selection

