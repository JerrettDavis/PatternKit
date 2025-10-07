# Decorator Pattern

> **Fluent wrapping and extension of components with layered behavior enhancements**

## Overview

The **Decorator** pattern allows you to attach additional responsibilities to an object dynamically. PatternKit's implementation provides a fluent, allocation-light way to wrap any component with ordered decorators that can:

- **Transform input** before it reaches the component (`Before`)
- **Transform output** after it returns from the component (`After`)
- **Wrap entire execution** with custom logic (`Around`)

Decorators are applied as layers in registration order, making it seamless to add cross-cutting concerns like logging, caching, validation, or error handling without modifying the original component.

## Mental Model

Think of decorators as **layers of an onion**:
- The **component** is at the center
- Each **decorator** wraps around it in the order registered
- Execution flows outward-to-inward for input transformation
- Then inward-to-outward for output transformation

```
┌─────────────────────────────────────┐
│ Before (outermost)                  │
│  ┌──────────────────────────────┐   │
│  │ Around                       │   │
│  │  ┌────────────────────────┐  │   │
│  │  │ After                  │  │   │
│  │  │  ┌──────────────────┐  │  │   │
│  │  │  │ Component (core) │  │  │   │
│  │  │  └──────────────────┘  │  │   │
│  │  └────────────────────────┘  │   │
│  └──────────────────────────────┘   │
└─────────────────────────────────────┘
```

## Quick Start

### Basic Decorator

```csharp
using PatternKit.Structural.Decorator;

// Wrap a simple component
var doubled = Decorator<int, int>.Create(static x => x * 2)
    .Build();

var result = doubled.Execute(5); // 10
```

### Before: Input Transformation

```csharp
// Validate/transform input before component execution
var validated = Decorator<int, int>.Create(static x => 100 / x)
    .Before(static x => x == 0 
        ? throw new ArgumentException("Cannot be zero") 
        : x)
    .Build();

var result = validated.Execute(5); // 20
// validated.Execute(0); // throws ArgumentException
```

### After: Output Transformation

```csharp
// Transform output after component execution
var enhanced = Decorator<string, int>.Create(static s => s.Length)
    .Before(static s => s.Trim())           // Remove whitespace first
    .After(static (input, length) => length * 2)  // Double the length
    .Build();

var result = enhanced.Execute("  hello  "); // 10 (trimmed "hello" = 5, doubled = 10)
```

### Around: Full Control

```csharp
// Add logging around execution
var logged = Decorator<int, int>.Create(static x => x * x)
    .Around((x, next) => {
        Console.WriteLine($"Input: {x}");
        var result = next(x);
        Console.WriteLine($"Output: {result}");
        return result;
    })
    .Build();

var squared = logged.Execute(7);
// Console output:
// Input: 7
// Output: 49
```

## API Reference

### Creating a Decorator

```csharp
public static Builder Create(Component component)
```

Creates a new builder for decorating a component.

**Parameters:**
- `component`: The base operation to decorate (signature: `TOut Component(TIn input)`)

**Returns:** A fluent `Builder` instance

### Builder Methods

#### Before(BeforeTransform transform)

Adds an input transformation decorator.

```csharp
public Builder Before(BeforeTransform transform)
```

**Signature:** `TIn BeforeTransform(TIn input)`

Multiple `Before` decorators are applied in registration order (outermost to innermost).

**Example:**
```csharp
.Before(static x => x + 10)
.Before(static x => x * 2)  // Applied after the first Before
```

#### After(AfterTransform transform)

Adds an output transformation decorator.

```csharp
public Builder After(AfterTransform transform)
```

**Signature:** `TOut AfterTransform(TIn input, TOut output)`

The `After` decorator receives both the original input and the output from inner layers.

**Example:**
```csharp
.After(static (input, result) => result + input)
```

#### Around(AroundTransform transform)

Adds a wrapper that controls execution flow.

```csharp
public Builder Around(AroundTransform transform)
```

**Signature:** `TOut AroundTransform(TIn input, Component next)`

The `Around` decorator has full control over whether and how the next layer is invoked.

**Example:**
```csharp
.Around((x, next) => {
    // Pre-processing
    var result = next(x);  // Invoke next layer
    // Post-processing
    return result;
})
```

#### Build()

Builds an immutable decorator instance.

```csharp
public Decorator<TIn, TOut> Build()
```

### Execution

```csharp
public TOut Execute(in TIn input)
```

Executes the decorated component with the given input, applying all decorators in order.

## Real-World Examples

### Caching Decorator

```csharp
var cache = new Dictionary<int, int>();
var cachedOperation = Decorator<int, int>.Create(x => ExpensiveComputation(x))
    .Around((x, next) => {
        if (cache.TryGetValue(x, out var cached))
            return cached;
        
        var result = next(x);
        cache[x] = result;
        return result;
    })
    .Build();

// First call: computes and caches
var result1 = cachedOperation.Execute(42); 

// Second call: returns cached value
var result2 = cachedOperation.Execute(42);
```

### Retry Logic

```csharp
var retriable = Decorator<string, HttpResponse>.Create(url => HttpClient.Get(url))
    .Around((url, next) => {
        int attempts = 0;
        Exception lastError = null;
        
        while (attempts < 3) {
            try {
                return next(url);
            } catch (Exception ex) {
                lastError = ex;
                attempts++;
                Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, attempts)));
            }
        }
        
        throw new Exception($"Failed after {attempts} attempts", lastError);
    })
    .Build();
```

### Performance Monitoring

```csharp
var monitored = Decorator<Query, Result>.Create(query => Database.Execute(query))
    .Around((query, next) => {
        var sw = Stopwatch.StartNew();
        try {
            var result = next(query);
            Metrics.RecordSuccess(query.Name, sw.Elapsed);
            return result;
        } catch (Exception ex) {
            Metrics.RecordFailure(query.Name, sw.Elapsed, ex);
            throw;
        }
    })
    .Build();
```

### Authorization + Audit

```csharp
var secured = Decorator<Request, Response>.Create(req => HandleRequest(req))
    .Before(req => {
        if (!req.User.HasPermission(req.Resource))
            throw new UnauthorizedException();
        return req;
    })
    .Around((req, next) => {
        AuditLog.LogAccess(req.User, req.Resource);
        var response = next(req);
        AuditLog.LogSuccess(req.User, req.Resource);
        return response;
    })
    .Build();
```

### Circuit Breaker

```csharp
var circuitBreaker = new CircuitBreakerState();

var protected = Decorator<Request, Response>.Create(req => ExternalService.Call(req))
    .Around((req, next) => {
        if (circuitBreaker.IsOpen)
            throw new CircuitBreakerOpenException();
        
        try {
            var result = next(req);
            circuitBreaker.RecordSuccess();
            return result;
        } catch (Exception ex) {
            circuitBreaker.RecordFailure();
            throw;
        }
    })
    .Build();
```

## Chaining Multiple Decorators

Decorators compose naturally—each decorator layer wraps the previous ones:

```csharp
var fullyDecorated = Decorator<int, int>.Create(static x => x * 2)
    .Before(static x => x + 1)              // Validate/transform input
    .Around((x, next) => {                   // Add caching
        if (cache.TryGetValue(x, out var cached))
            return cached;
        var result = next(x);
        cache[x] = result;
        return result;
    })
    .Around((x, next) => {                   // Add logging
        Log($"Calling with {x}");
        var result = next(x);
        Log($"Returned {result}");
        return result;
    })
    .After(static (input, result) => result * 10)  // Transform output
    .Build();

// Execution order:
// 1. Before: 5 + 1 = 6
// 2. Around (logging): Log "Calling with 6"
// 3. Around (caching): Check cache, miss
// 4. Component: 6 * 2 = 12
// 5. After: 12 * 10 = 120
// 6. Around (logging): Log "Returned 120"
// 7. Return: 120
```

## Execution Order

### Multiple Before Decorators

Applied in **registration order** (first registered = outermost):

```csharp
.Before(x => x + 10)  // Applied first
.Before(x => x * 2)   // Applied second to the result of first
```

Input `5` → `15` → `30` → component

### Multiple After Decorators

Applied in **registration order** as layers (first registered = outermost):

```csharp
.After((_, r) => r + 10)  // Receives result from inner layer
.After((_, r) => r * 2)   // Receives result from component
```

Component returns `10` → second After makes it `20` → first After makes it `30`

### Mixed Decorators

```csharp
.Before(x => x + 1)       // Layer 0 (outermost for input)
.Around((x, next) => ...)  // Layer 1
.After((_, r) => ...)      // Layer 2 (outermost for output)
```

## Performance Characteristics

- **Immutable after build**: Thread-safe for concurrent reuse
- **Minimal allocations**: Decorators stored as arrays
- **No reflection**: Direct delegate invocations
- **Struct-friendly**: Can decorate value types efficiently

## Best Practices

### 1. Use Static Lambdas Where Possible

```csharp
// ✅ Good: No closure allocation
.Before(static x => x + 10)

// ❌ Avoid: Captures variable
int offset = 10;
.Before(x => x + offset)
```

### 2. Separate Concerns

Each decorator should have a single responsibility:

```csharp
// ✅ Good: Separate decorators for separate concerns
var result = Decorator<T, R>.Create(component)
    .Around(AddLogging)
    .Around(AddCaching)
    .Around(AddRetry)
    .Build();

// ❌ Avoid: Multiple concerns in one decorator
.Around((x, next) => {
    Log();
    CheckCache();
    Retry(() => next(x));
})
```

### 3. Order Matters

Consider the execution flow when ordering decorators:

```csharp
// Cache should be checked BEFORE expensive retry logic
.Around(AddCaching)
.Around(AddRetry)

// Validation should happen BEFORE any processing
.Before(ValidateInput)
.Around(AddProcessing)
```

### 4. Reuse Decorators

Build once, execute many times:

```csharp
// ✅ Good
var decorator = Decorator<int, int>.Create(...).Build();
for (int i = 0; i < 1000; i++)
    decorator.Execute(i);

// ❌ Avoid: Rebuilding on each use
for (int i = 0; i < 1000; i++)
    Decorator<int, int>.Create(...).Build().Execute(i);
```

## Comparison with Traditional Decorator

### Traditional Approach

```csharp
public interface IComponent {
    int Execute(int input);
}

public class ConcreteComponent : IComponent {
    public int Execute(int input) => input * 2;
}

public class LoggingDecorator : IComponent {
    private readonly IComponent _component;
    public LoggingDecorator(IComponent component) => _component = component;
    
    public int Execute(int input) {
        Console.WriteLine($"Input: {input}");
        var result = _component.Execute(input);
        Console.WriteLine($"Output: {result}");
        return result;
    }
}

// Usage
IComponent component = new ConcreteComponent();
component = new LoggingDecorator(component);
component = new CachingDecorator(component);
var result = component.Execute(5);
```

### PatternKit Approach

```csharp
var component = Decorator<int, int>.Create(static x => x * 2)
    .Around((x, next) => {
        Console.WriteLine($"Input: {x}");
        var result = next(x);
        Console.WriteLine($"Output: {result}");
        return result;
    })
    .Around(AddCaching)
    .Build();

var result = component.Execute(5);
```

**Benefits:**
- No interface/class hierarchy needed
- Fluent, discoverable API
- Easier to compose and reorder
- Less boilerplate code
- Type-safe with full IntelliSense support

## See Also

- [Strategy Pattern](../../behavioral/strategy/strategy.md) - For conditional logic
- [Chain of Responsibility](../../behavioral/chain/actionchain.md) - For sequential processing
- [Adapter Pattern](../adapter/fluent-adapter.md) - For type conversion
- [Composite Pattern](../composite/composite.md) - For tree structures

