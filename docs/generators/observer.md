# Observer Pattern Generator

## Overview

The **Observer Generator** creates type-safe, high-performance Observer pattern implementations with configurable threading, exception handling, and ordering semantics. It eliminates the need to manually write subscription management code, providing compile-time safety and optimal runtime performance.

## When to Use

Use the Observer generator when you need:

- **Event notification systems**: Publish events to multiple subscribers
- **Reactive programming**: Build observable data streams and change notifications
- **Decoupled communication**: Publishers don't need to know about subscribers
- **Type-safe event handling**: Compile-time verification of handler signatures
- **Configurable behavior**: Control threading, exceptions, and ordering

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

```csharp
using PatternKit.Generators.Observer;

public record Temperature(double Celsius);

[Observer(typeof(Temperature))]
public partial class TemperatureChanged
{
}
```

Generated methods:

```csharp
public partial class TemperatureChanged
{
    // Subscribe with sync handler
    public IDisposable Subscribe(Action<Temperature> handler) { ... }
    
    // Subscribe with async handler
    public IDisposable Subscribe(Func<Temperature, ValueTask> handler) { ... }
    
    // Publish to all subscribers
    public void Publish(Temperature payload) { ... }
    
    // Publish asynchronously
    public ValueTask PublishAsync(Temperature payload, CancellationToken cancellationToken = default) { ... }
}
```

Usage:

```csharp
var tempEvent = new TemperatureChanged();

// Subscribe to events
var subscription = tempEvent.Subscribe(temp => 
    Console.WriteLine($"Temperature: {temp.Celsius}°C"));

// Publish events
tempEvent.Publish(new Temperature(23.5));
tempEvent.Publish(new Temperature(24.0));

// Unsubscribe
subscription.Dispose();
```

## Basic Usage

### Synchronous Handlers

```csharp
public record StockPrice(string Symbol, decimal Price);

[Observer(typeof(StockPrice))]
public partial class StockPriceChanged
{
}

// Usage
var priceEvent = new StockPriceChanged();

priceEvent.Subscribe(price => 
    Console.WriteLine($"{price.Symbol}: ${price.Price}"));

priceEvent.Subscribe(price => 
    LogToDatabase(price));

priceEvent.Publish(new StockPrice("MSFT", 420.50m));
```

### Asynchronous Handlers

```csharp
public record UserRegistration(string Email, DateTime Timestamp);

[Observer(typeof(UserRegistration))]
public partial class UserRegistered
{
}

// Usage
var userEvent = new UserRegistered();

userEvent.Subscribe(async user =>
{
    await SendWelcomeEmailAsync(user.Email);
    await CreateUserProfileAsync(user);
});

await userEvent.PublishAsync(
    new UserRegistration("user@example.com", DateTime.UtcNow));
```

### Managing Subscriptions

Subscriptions return `IDisposable` for cleanup:

```csharp
var subscription1 = tempEvent.Subscribe(t => Console.WriteLine(t.Celsius));
var subscription2 = tempEvent.Subscribe(t => LogTemperature(t));

// Unsubscribe individual handlers
subscription1.Dispose();

// Using 'using' for automatic cleanup
using (var sub = tempEvent.Subscribe(t => ProcessTemperature(t)))
{
    tempEvent.Publish(new Temperature(25.0));
} // Automatically unsubscribed
```

## Configuration Options

### Threading Policies

Control how Subscribe/Publish operations handle concurrency:

#### Locking (Default)

Uses locking for thread safety. Recommended for most scenarios:

```csharp
[Observer(typeof(Temperature), Threading = ObserverThreadingPolicy.Locking)]
public partial class TemperatureChanged { }
```

**Characteristics:**
- Thread-safe Subscribe/Unsubscribe/Publish
- Snapshots subscriber list under lock for predictable iteration
- Moderate overhead for lock acquisition

**Use when:**
- Multiple threads may publish or subscribe concurrently
- You need deterministic ordering
- Default choice for most applications

#### SingleThreadedFast

No thread safety, maximum performance:

```csharp
[Observer(typeof(UiEvent), Threading = ObserverThreadingPolicy.SingleThreadedFast)]
public partial class UiEventOccurred { }
```

**Characteristics:**
- No synchronization overhead
- Not thread-safe
- Lowest memory footprint

**Use when:**
- All operations occur on a single thread (e.g., UI thread)
- Performance is critical
- You can guarantee no concurrent access

⚠️ **Warning:** Using this policy with concurrent access will cause data corruption and race conditions.

#### Concurrent

Lock-free atomic operations for high concurrency:

```csharp
[Observer(typeof(MetricUpdate), Threading = ObserverThreadingPolicy.Concurrent)]
public partial class MetricUpdated { }
```

**Characteristics:**
- Lock-free concurrent operations
- Thread-safe with better performance under high concurrency
- May have undefined ordering unless RegistrationOrder is used

**Use when:**
- High-throughput scenarios with many concurrent publishers
- Minimizing lock contention is important
- Can tolerate potential ordering variations

### Exception Policies

Control how exceptions from handlers are managed:

#### Continue (Default)

Continue invoking all handlers even if some throw:

```csharp
[Observer(typeof(Message), Exceptions = ObserverExceptionPolicy.Continue)]
public partial class MessageReceived
{
    // Optional: handle errors from subscribers
    partial void OnSubscriberError(Exception ex)
    {
        Logger.LogError(ex, "Subscriber failed");
    }
}
```

**Characteristics:**
- All handlers get invoked
- Exceptions are caught and optionally logged
- Publishing never throws

**Use when:**
- Subscriber failures shouldn't affect other subscribers
- You want best-effort delivery
- Fault tolerance is important

**Optional Hook:** Implement `partial void OnSubscriberError(Exception ex)` to log or handle errors.

#### Stop

Stop at first exception and rethrow:

```csharp
[Observer(typeof(CriticalCommand), Exceptions = ObserverExceptionPolicy.Stop)]
public partial class CommandExecuted { }
```

**Characteristics:**
- First exception stops publishing
- Exception is rethrown to caller
- Remaining handlers are not invoked

**Use when:**
- Any handler failure should abort the operation
- You need to handle errors at the call site
- Order matters and failures are critical

#### Aggregate

Collect all exceptions and throw AggregateException:

```csharp
[Observer(typeof(ValidationRequest), Exceptions = ObserverExceptionPolicy.Aggregate)]
public partial class ValidationRequested { }
```

**Characteristics:**
- All handlers are invoked
- Exceptions are collected
- AggregateException thrown if any failed

**Use when:**
- You need to know about all failures
- All handlers should run regardless of failures
- Collecting multiple validation errors

```csharp
try
{
    validationEvent.Publish(request);
}
catch (AggregateException aex)
{
    foreach (var ex in aex.InnerExceptions)
    {
        Console.WriteLine($"Validation error: {ex.Message}");
    }
}
```

### Order Policies

Control handler invocation order:

#### RegistrationOrder (Default)

Handlers invoked in subscription order (FIFO):

```csharp
[Observer(typeof(Event), Order = ObserverOrderPolicy.RegistrationOrder)]
public partial class EventOccurred { }
```

**Characteristics:**
- Deterministic, predictable order
- Handlers invoked in the order they were subscribed
- Slightly higher memory overhead

**Use when:**
- Order matters (e.g., validation → processing → logging)
- Debugging requires predictable behavior
- Default choice for most scenarios

#### Undefined

No order guarantee, potential performance benefit:

```csharp
[Observer(typeof(Metric), Order = ObserverOrderPolicy.Undefined)]
public partial class MetricRecorded { }
```

**Characteristics:**
- No ordering guarantee
- May provide better performance with Concurrent threading
- Lower memory overhead

**Use when:**
- Order doesn't matter (e.g., independent metrics collection)
- Maximum performance is needed
- Handlers are truly independent

### Async Configuration

Control async method generation:

```csharp
// Generate async methods (default)
[Observer(typeof(Data), GenerateAsync = true)]
public partial class DataAvailable { }

// Don't generate async methods
[Observer(typeof(Data), GenerateAsync = false)]
public partial class DataAvailable { }

// Force async-only (no sync Subscribe)
[Observer(typeof(Data), ForceAsync = true)]
public partial class DataAvailable { }
```

## Supported Types

The generator supports:

| Type | Supported | Example / Notes |
|------|-----------|------------------|
| `partial class` | ✅ | `public partial class Event { }` |
| `partial struct` | ❌ | Generates PKOBS003 diagnostic (struct observers are not supported) |
| `partial record class` | ✅ | `public partial record class Event;` |
| `partial record struct` | ❌ | Generates PKOBS003 diagnostic (struct observers are not supported) |
| Non-partial types | ❌ | Generates PKOBS001 error |

> **Note:** Struct-based observer types (`partial struct`, `partial record struct`) are rejected with PKOBS003 diagnostic. Supporting struct observers would require complex capture and boxing semantics, so only class-based observer types are currently supported.

## API Reference

### Subscribe Methods

#### Synchronous Handler

```csharp
public IDisposable Subscribe(Action<TPayload> handler)
```

Subscribes a synchronous handler to the event.

**Parameters:**
- `handler`: Action to invoke when events are published

**Returns:** `IDisposable` that removes the subscription when disposed

**Example:**
```csharp
var sub = observable.Subscribe(payload => 
    Console.WriteLine(payload));
sub.Dispose(); // Unsubscribe
```

#### Asynchronous Handler

```csharp
public IDisposable Subscribe(Func<TPayload, ValueTask> handler)
```

Subscribes an asynchronous handler to the event.

**Parameters:**
- `handler`: Async function to invoke when events are published

**Returns:** `IDisposable` that removes the subscription when disposed

**Example:**
```csharp
var sub = observable.Subscribe(async payload =>
    await ProcessAsync(payload));
```

### Publish Methods

#### Synchronous Publish

```csharp
public void Publish(TPayload payload)
```

Publishes an event to all subscribers synchronously.

**Parameters:**
- `payload`: The event data to publish

**Behavior:**
- Invokes synchronous handlers directly (exception handling follows configured policy)
- Invokes async handlers asynchronously in fire-and-forget mode:
  - `Continue` policy: exceptions routed to `OnSubscriberError` hook
  - `Stop` policy: exceptions are unobserved (cannot stop synchronous execution)
  - `Aggregate` policy: exceptions logged via `OnSubscriberError` (cannot aggregate synchronously)
  - For deterministic exception behavior with async handlers, use `PublishAsync` instead

**Example:**
```csharp
observable.Publish(new Temperature(25.0));
```

#### Asynchronous Publish

```csharp
public ValueTask PublishAsync(TPayload payload, CancellationToken cancellationToken = default)
```

Publishes an event to all subscribers asynchronously.

**Parameters:**
- `payload`: The event data to publish
- `cancellationToken`: Optional cancellation token

**Returns:** `ValueTask` that completes when all async handlers finish

**Behavior:**
- Waits for async handlers to complete
- Synchronous handlers run on calling thread
- Exception handling per configured policy
- Honors cancellation token

**Example:**
```csharp
await observable.PublishAsync(
    new UserAction("click"), 
    cancellationToken);
```

### Optional Hooks

#### OnSubscriberError

```csharp
partial void OnSubscriberError(Exception ex);
```

Optional method for handling subscriber exceptions. Primarily used with `Exceptions = ObserverExceptionPolicy.Continue`, but also invoked for fire-and-forget async handler exceptions in sync `Publish` under the `Aggregate` policy (since those cannot be aggregated synchronously).

**Parameters:**
- `ex`: The exception thrown by a subscriber

**Example:**
```csharp
[Observer(typeof(Event), Exceptions = ObserverExceptionPolicy.Continue)]
public partial class EventOccurred
{
    partial void OnSubscriberError(Exception ex)
    {
        Logger.LogError(ex, "Subscriber threw exception");
        Telemetry.RecordError(ex);
    }
}
```

## Performance Considerations

### Memory and Allocations

- **SingleThreadedFast**: Uses `List<T>`, minimal allocations
- **Locking**: Uses `List<T>` with lock, snapshots on publish
- **Concurrent**: Uses `ImmutableList` (RegistrationOrder) or `ConcurrentBag` (Undefined). When using `ImmutableList`, the generated code depends on `System.Collections.Immutable`; for TFMs that don't reference it by default (for example, `netstandard2.0`), you may need to add an explicit `System.Collections.Immutable` package reference.

### Thread Safety Overhead

| Policy | Subscribe/Unsubscribe | Publish | Notes |
|--------|----------------------|---------|-------|
| SingleThreadedFast | None | None | Fastest, not thread-safe |
| Locking | Lock acquisition | Snapshot + lock | Good for moderate concurrency |
| Concurrent | Atomic operations | Lock-free | Best for high concurrency |

### Async Performance

- `PublishAsync` uses `ValueTask` to reduce allocations
- Synchronous handlers in `PublishAsync` don't allocate
- Async handlers only allocate if they don't complete synchronously

### Best Practices

1. **Use Locking by default** unless you have specific needs
2. **Profile before optimizing** - start with defaults
3. **Dispose subscriptions** to prevent memory leaks
4. **Use SingleThreadedFast** only when guaranteed single-threaded
5. **Prefer Continue exception policy** for fault tolerance
6. **Use weak references** if subscribers have long lifetimes and publishers are short-lived (implement manually)

## Common Patterns

### Observable Properties

```csharp
public record PropertyChanged(string PropertyName, object? NewValue);

[Observer(typeof(PropertyChanged))]
public partial class PropertyChangeNotifier
{
}

public class ViewModel
{
    private readonly PropertyChangeNotifier _notifier = new();
    private string _name = "";
    
    public IDisposable SubscribeToChanges(Action<PropertyChanged> handler) =>
        _notifier.Subscribe(handler);
    
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                _notifier.Publish(new PropertyChanged(nameof(Name), value));
            }
        }
    }
}
```

### Event Aggregator

```csharp
// Note: Observer types must be top-level; nested observers are not supported (PKOBS003)
[Observer(typeof(UserLoggedIn))]
public partial class UserLoggedInEvent { }

[Observer(typeof(OrderPlaced))]
public partial class OrderPlacedEvent { }

public partial class EventAggregator
{
    private readonly UserLoggedInEvent _userLoggedIn = new();
    private readonly OrderPlacedEvent _orderPlaced = new();
    
    public IDisposable Subscribe<T>(Action<T> handler)
    {
        return typeof(T).Name switch
        {
            nameof(UserLoggedIn) => _userLoggedIn.Subscribe(e => handler((T)(object)e)),
            nameof(OrderPlaced) => _orderPlaced.Subscribe(e => handler((T)(object)e)),
            _ => throw new NotSupportedException()
        };
    }
    
    public void Publish<T>(T @event)
    {
        switch (@event)
        {
            case UserLoggedIn e: _userLoggedIn.Publish(e); break;
            case OrderPlaced e: _orderPlaced.Publish(e); break;
        }
    }
}
```

### Composite Subscriptions

```csharp
public class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _subscriptions = new();
    
    public void Add(IDisposable subscription) => _subscriptions.Add(subscription);
    
    public void Dispose()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
    }
}

// Usage
var subscriptions = new CompositeDisposable();
subscriptions.Add(tempEvent.Subscribe(HandleTemperature));
subscriptions.Add(pressureEvent.Subscribe(HandlePressure));
subscriptions.Add(humidityEvent.Subscribe(HandleHumidity));

// Unsubscribe all at once
subscriptions.Dispose();
```

## Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| **PKOBS001** | Error | Type marked with `[Observer]` must be declared as `partial` |
| **PKOBS002** | Error | Unable to extract payload type from `[Observer]` attribute |
| **PKOBS003** | Warning | Invalid configuration or unsupported type (generic, nested, or struct types) |

### PKOBS001: Type must be partial

**Cause:** Missing `partial` keyword on observer type.

**Fix:**
```csharp
// ❌ Wrong
[Observer(typeof(Message))]
public class MessageReceived { }

// ✅ Correct
[Observer(typeof(Message))]
public partial class MessageReceived { }
```

### PKOBS002: Missing payload type

**Cause:** Payload type could not be determined from attribute.

**Fix:** Ensure you provide a valid type to the attribute:
```csharp
// ✅ Correct
[Observer(typeof(MyEventData))]
public partial class MyEvent { }
```

## Best Practices

### 1. Always Dispose Subscriptions

Prevent memory leaks by disposing subscriptions:

```csharp
// ✅ Good: Using statement
using var subscription = observable.Subscribe(HandleEvent);

// ✅ Good: Explicit disposal
var subscription = observable.Subscribe(HandleEvent);
// ... later ...
subscription.Dispose();

// ⚠️ Bad: Never disposed - memory leak!
observable.Subscribe(HandleEvent);
```

### 2. Use Immutable Payload Types

Records make excellent event payloads:

```csharp
// ✅ Good: Immutable record
public record OrderPlaced(int OrderId, decimal Amount, DateTime Timestamp);

[Observer(typeof(OrderPlaced))]
public partial class OrderPlacedEvent { }

// ⚠️ Avoid: Mutable payload
public class OrderPlaced
{
    public int OrderId { get; set; }  // Can be modified by handlers
}
```

### 3. Keep Handlers Fast

Long-running handlers block other subscribers:

```csharp
// ⚠️ Bad: Slow handler blocks others
observable.Subscribe(data => 
{
    Thread.Sleep(1000);  // Blocks!
    ProcessData(data);
});

// ✅ Good: Offload work
observable.Subscribe(data => 
    Task.Run(() => ProcessData(data)));

// ✅ Better: Use async
observable.Subscribe(async data =>
    await ProcessDataAsync(data));
```

### 4. Choose the Right Threading Policy

```csharp
// ✅ UI thread events
[Observer(typeof(UiEvent), Threading = ObserverThreadingPolicy.SingleThreadedFast)]

// ✅ General application events
[Observer(typeof(AppEvent), Threading = ObserverThreadingPolicy.Locking)]

// ✅ High-throughput metrics
[Observer(typeof(Metric), Threading = ObserverThreadingPolicy.Concurrent)]
```

### 5. Handle Exceptions Appropriately

```csharp
// ✅ Good: Fault tolerant
[Observer(typeof(Notification), Exceptions = ObserverExceptionPolicy.Continue)]
public partial class NotificationSent
{
    partial void OnSubscriberError(Exception ex)
    {
        Logger.LogWarning(ex, "Notification handler failed");
    }
}

// ✅ Good: Critical operations
[Observer(typeof(Payment), Exceptions = ObserverExceptionPolicy.Stop)]
public partial class PaymentProcessed { }
```

### 6. Use Meaningful Event Names

```csharp
// ✅ Good: Clear, action-based names
[Observer(typeof(User))]
public partial class UserRegistered { }

[Observer(typeof(Order))]
public partial class OrderShipped { }

// ⚠️ Unclear
[Observer(typeof(User))]
public partial class UserEvent { }  // What happened to the user?
```

## Examples

See the [ObserverGeneratorDemo](/src/PatternKit.Examples/ObserverGeneratorDemo/) for complete, runnable examples including:

- **TemperatureMonitor.cs**: Basic observer usage with temperature sensors
- **NotificationSystem.cs**: Async handlers and exception handling
- **README.md**: Example explanations and usage

## Troubleshooting

### Handlers not being called

**Possible causes:**
1. Subscription was disposed
2. Wrong payload type
3. Exception thrown and swallowed (check `OnSubscriberError`)

**Debug steps:**
```csharp
var sub = observable.Subscribe(payload =>
{
    Console.WriteLine("Handler called!");  // Add logging
});
observable.Publish(payload);
```

### Memory leaks

**Cause:** Subscriptions not disposed.

**Fix:** Always dispose subscriptions, especially in long-lived objects:
```csharp
public class Service : IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();
    
    public Service(SomeObservable observable)
    {
        _subscriptions.Add(observable.Subscribe(HandleEvent));
    }
    
    public void Dispose() => _subscriptions.Dispose();
}
```

### Race conditions with SingleThreadedFast

**Cause:** Using SingleThreadedFast with multiple threads.

**Fix:** Use `Locking` or `Concurrent` policy:
```csharp
[Observer(typeof(Data), Threading = ObserverThreadingPolicy.Locking)]
public partial class DataReceived { }
```

### Async handlers not awaited in Publish

**Behavior:** `Publish` calls async handlers in fire-and-forget mode.

**Solution:** Use `PublishAsync` to await async handlers:
```csharp
// ⚠️ Async handlers not awaited
observable.Publish(data);

// ✅ Async handlers are awaited
await observable.PublishAsync(data);
```

## See Also

- [Memento Generator](memento.md) — For saving/restoring observable state
- [State Machine Generator](state-machine.md) — For state-based event handling
- [Observer Pattern (Classic)](https://en.wikipedia.org/wiki/Observer_pattern)
- [Reactive Extensions](https://reactivex.io/) — Advanced reactive programming
