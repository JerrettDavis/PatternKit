# Observer Pattern API Reference

Complete API documentation for the Observer pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.Observer;
```

---

## Observer\<TEvent\>

Thread-safe event hub for broadcasting typed events.

```csharp
public sealed class Observer<TEvent>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TEvent` | The event type to broadcast |

### Delegates

#### `Handler`

```csharp
public delegate void Handler(in TEvent @event);
```

Processes a published event.

#### `Filter`

```csharp
public delegate bool Filter(in TEvent @event);
```

Determines if a handler should receive an event.

#### `ErrorSink`

```csharp
public delegate void ErrorSink(Exception ex, in TEvent @event);
```

Handles errors from failed handlers.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Publish(in TEvent @event)` | `void` | Broadcasts event to all matching subscribers |
| `Subscribe(Handler handler)` | `IDisposable` | Subscribes to all events |
| `Subscribe(Filter filter, Handler handler)` | `IDisposable` | Subscribes with predicate filter |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Example

```csharp
var hub = Observer<OrderEvent>.Create()
    .OnError((ex, in e) => Log.Error(ex, "Handler failed for {OrderId}", e.OrderId))
    .ThrowAggregate()
    .Build();

// Subscribe to all events
var sub1 = hub.Subscribe((in OrderEvent e) => ProcessOrder(e));

// Subscribe with filter
var sub2 = hub.Subscribe(
    (in OrderEvent e) => e.Type == OrderEventType.Urgent,
    (in OrderEvent e) => HandleUrgent(e));

// Publish
hub.Publish(new OrderEvent { OrderId = "123", Type = OrderEventType.Urgent });

// Unsubscribe
sub1.Dispose();
sub2.Dispose();
```

---

## Observer\<TEvent\>.Builder

Fluent builder for configuring the observer.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `OnError(ErrorSink sink)` | `Builder` | Sets error handler (optional) |
| `ThrowAggregate()` | `Builder` | Collects all exceptions, throws `AggregateException` |
| `ThrowFirstError()` | `Builder` | Throws immediately on first exception |
| `SwallowErrors()` | `Builder` | Never throws, errors go to sink only |
| `Build()` | `Observer<TEvent>` | Builds the immutable observer |

### Error Policy Details

| Policy | Handler Continuation | Exception Behavior |
|--------|---------------------|-------------------|
| `ThrowAggregate` | All run | Single `AggregateException` at end |
| `ThrowFirstError` | Stops on first | Immediate throw |
| `SwallowErrors` | All run | No throw, sink receives errors |

### Example

```csharp
// Aggregate errors (default)
var hub1 = Observer<Event>.Create()
    .OnError((ex, in e) => Log.Warn(ex.Message))
    .ThrowAggregate()
    .Build();

// Fail fast
var hub2 = Observer<Event>.Create()
    .ThrowFirstError()
    .Build();

// Never throw
var hub3 = Observer<Event>.Create()
    .OnError((ex, in e) => Log.Error(ex.Message))
    .SwallowErrors()
    .Build();
```

---

## Subscription Management

Subscriptions return `IDisposable`. Disposing is idempotent:

```csharp
var sub = hub.Subscribe((in Event e) => Handle(e));

// Unsubscribe
sub.Dispose();
sub.Dispose(); // Safe to call multiple times
```

### Subscription Lifetime

- Subscriptions are held by the hub until disposed
- Disposing during `Publish` affects subsequent publishes only
- Re-subscribing creates a new subscription

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` | No - use from single thread |
| `Observer<TEvent>` | Yes - immutable after build |
| `Subscribe` | Yes - atomic |
| `Publish` | Yes - reads snapshot |
| `Dispose` (subscription) | Yes - atomic |

### Implementation Notes

- Copy-on-write subscriptions: publishing is contention-free
- Subscribe/unsubscribe perform atomic array swap
- Handlers invoked in registration order

---

## Complete Example

```csharp
using PatternKit.Behavioral.Observer;

// Define event type
public record StockPriceChanged(string Symbol, decimal Price, DateTime Timestamp);

// Create hub
var hub = Observer<StockPriceChanged>.Create()
    .OnError((ex, in e) => Console.Error.WriteLine($"Error handling {e.Symbol}: {ex.Message}"))
    .ThrowAggregate()
    .Build();

// UI subscriber - all updates
var uiSub = hub.Subscribe((in StockPriceChanged e) =>
    Console.WriteLine($"[UI] {e.Symbol}: ${e.Price}"));

// Alert subscriber - large movements only
var alertSub = hub.Subscribe(
    (in StockPriceChanged e) => e.Price > 100,
    (in StockPriceChanged e) => Console.WriteLine($"[ALERT] {e.Symbol} exceeded $100!"));

// Logging subscriber
var logSub = hub.Subscribe((in StockPriceChanged e) =>
    File.AppendAllText("prices.log", $"{e.Timestamp}: {e.Symbol} = {e.Price}\n"));

// Publish events
hub.Publish(new StockPriceChanged("AAPL", 150.50m, DateTime.UtcNow));
hub.Publish(new StockPriceChanged("GOOG", 95.00m, DateTime.UtcNow));

// Cleanup
uiSub.Dispose();
alertSub.Dispose();
logSub.Dispose();
```

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
