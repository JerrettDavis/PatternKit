# Observer Pattern Generator

The Observer Pattern Generator automatically creates subscribe/publish infrastructure for event-driven communication. It eliminates boilerplate for observer management while providing thread-safe subscription handling, snapshot-based iteration, and configurable exception policies.

## Overview

The generator produces:

- **Subscribe method** returning IDisposable for automatic unsubscription
- **Publish method** for synchronous notification with snapshot semantics
- **PublishAsync method** for asynchronous notification (when enabled)
- **Async Subscribe** accepting Func<T, ValueTask> handlers (when async enabled)
- **Thread-safe** subscription management with configurable threading policies
- **Exception policies** for controlling error propagation during publish

## Quick Start

### 1. Define Your Event Stream

Mark your event stream class with `[Observer]` and specify the payload type:

```csharp
using PatternKit.Generators.Observer;

public record OrderEvent(string OrderId, decimal Amount);

[Observer(typeof(OrderEvent))]
public partial class OrderEventStream { }
```

### 2. Build Your Project

The generator runs during compilation and produces Subscribe/Publish methods:

```csharp
var stream = new OrderEventStream();

// Subscribe - returns IDisposable
var sub = stream.Subscribe(e => Console.WriteLine($"Order: {e.OrderId}"));

// Publish
stream.Publish(new OrderEvent("ORD-1", 99.99m));

// Unsubscribe
sub.Dispose();
```

### 3. Generated Code

```csharp
partial class OrderEventStream
{
    private readonly List<(int Id, Action<OrderEvent> Handler)> _syncSubscribers = new();
    private readonly object _subscriberLock = new();
    private int _nextId;

    public IDisposable Subscribe(Action<OrderEvent> handler) { ... }
    public void Publish(OrderEvent payload) { ... }
}
```

## Attributes

### [Observer]

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `payloadType` (ctor) | `Type` | required | The event payload type |
| `Threading` | `ObserverThreadingPolicy` | `Locking` | Thread safety model |
| `Exceptions` | `ObserverExceptionPolicy` | `Continue` | Exception handling during publish |
| `Order` | `ObserverOrderPolicy` | `RegistrationOrder` | Subscriber notification order |
| `GenerateAsync` | `bool` | `false` | Generate async Subscribe/PublishAsync |
| `ForceAsync` | `bool` | `false` | Force async generation |

### Threading Policies

- **SingleThreadedFast**: No synchronization. Best performance for single-threaded scenarios.
- **Locking** (default): Lock-based synchronization for thread-safe subscribe/publish.
- **Concurrent**: ConcurrentDictionary-based storage for high-contention scenarios.

### Exception Policies

- **Stop**: First subscriber exception immediately propagates. Remaining subscribers are not notified.
- **Continue** (default): All subscribers are notified. First exception is rethrown after completion.
- **Aggregate**: All subscribers are notified. All exceptions are aggregated into `AggregateException`.

### Order Policies

- **RegistrationOrder** (default): Subscribers notified in subscription order.
- **Undefined**: No ordering guarantee, may improve performance.

## Diagnostics

| Code | Message | Resolution |
|------|---------|------------|
| PKOBS001 | Type must be partial | Add `partial` keyword to type declaration |
| PKOBS004 | Async unsupported with SingleThreadedFast | Use Locking or Concurrent threading |
| PKOBS005 | Invalid configuration combination | Check attribute property combinations |

## Examples

### Basic Subscribe/Publish

```csharp
[Observer(typeof(string))]
public partial class MessageBus { }

var bus = new MessageBus();
var sub = bus.Subscribe(msg => Console.WriteLine(msg));
bus.Publish("Hello, world!");
sub.Dispose();
```

### Async Subscribe/Publish

```csharp
[Observer(typeof(OrderEvent), ForceAsync = true)]
public partial class OrderStream { }

var stream = new OrderStream();
stream.Subscribe(async e =>
{
    await SaveToDatabase(e);
});
await stream.PublishAsync(new OrderEvent("ORD-1", 100m));
```

### Aggregate Exception Handling

```csharp
[Observer(typeof(string), Exceptions = ObserverExceptionPolicy.Aggregate)]
public partial class ResilientBus { }

var bus = new ResilientBus();
bus.Subscribe(_ => throw new Exception("Sub1 failed"));
bus.Subscribe(_ => throw new Exception("Sub2 failed"));

try
{
    bus.Publish("test");
}
catch (AggregateException ex)
{
    // Contains both exceptions
    Console.WriteLine($"{ex.InnerExceptions.Count} failures");
}
```

## Best Practices

### 1. Always Dispose Subscriptions

Use `using` statements or store the IDisposable to prevent memory leaks:

```csharp
using var sub = stream.Subscribe(handler);
```

### 2. Choose the Right Threading Policy

- Use `SingleThreadedFast` for UI event loops or single-threaded services.
- Use `Locking` (default) for general-purpose thread safety.
- Use `Concurrent` for high-throughput scenarios with many concurrent publishers.

### 3. Use Continue Policy for Resilient Systems

The `Continue` exception policy ensures all subscribers are notified even if some fail, which is important for decoupled event-driven architectures.

### 4. Snapshot Semantics

Publish always iterates a stable snapshot of subscribers. Subscribe/unsubscribe calls during publish do not affect the current notification round. This prevents race conditions and unexpected behavior.

## See Also

- [Observer Generator Demo](../examples/observer-generator-demo.md)
- [Observer In-Process Event Hub Example](../examples/observer-demo.md)
- [API Reference](../api/PatternKit.Generators.Observer.html)
- [Troubleshooting](troubleshooting.md)
