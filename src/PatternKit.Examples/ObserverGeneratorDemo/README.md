# Observer Generator Examples

This directory contains comprehensive examples demonstrating the Observer pattern source generator.

## Examples Overview

### 1. TemperatureMonitor.cs

Demonstrates fundamental Observer pattern usage with a temperature monitoring system.

**Key Concepts:**
- Basic `[Observer(typeof(T))]` attribute usage
- Synchronous event handling
- Multiple subscribers to the same event
- Exception handling with `OnSubscriberError` hook
- Subscription lifecycle management (Subscribe/Dispose)
- Default configuration (Locking, Continue, RegistrationOrder)

**Demos Included:**
- `TemperatureMonitorDemo.Run()` - Complete monitoring system with alerts
- `MultipleSubscribersDemo.Run()` - Multiple handlers with fault tolerance
- `SubscriptionLifecycleDemo.Run()` - Subscription management patterns

**Run Example:**
```csharp
TemperatureMonitorDemo.Run();
MultipleSubscribersDemo.Run();
SubscriptionLifecycleDemo.Run();
```

### 2. NotificationSystem.cs

Demonstrates advanced Observer features with a multi-channel notification system.

**Key Concepts:**
- Async event handlers with `Func<T, ValueTask>`
- `PublishAsync` for awaiting async handlers
- Exception policies: Continue vs Aggregate
- Mixing sync and async handlers
- Cancellation token support
- Real-world async patterns (email, SMS, push notifications)

**Demos Included:**
- `AsyncNotificationDemo.RunAsync()` - Multi-channel async notifications
- `ExceptionHandlingDemo.Run()` - Exception policy comparison
- `MixedHandlersDemo.RunAsync()` - Sync and async handlers together
- `CancellationDemo.RunAsync()` - Cancellation token propagation

**Run Example:**
```csharp
await AsyncNotificationDemo.RunAsync();
ExceptionHandlingDemo.Run();
await MixedHandlersDemo.RunAsync();
await CancellationDemo.RunAsync();
```

## Quick Start

### Basic Usage

```csharp
// Define your event payload
public record TemperatureReading(string SensorId, double Celsius, DateTime Timestamp);

// Generate Observer implementation
[Observer(typeof(TemperatureReading))]
public partial class TemperatureChanged
{
}

// Use it
var tempEvent = new TemperatureChanged();

// Subscribe
var subscription = tempEvent.Subscribe(reading =>
{
    Console.WriteLine($"{reading.SensorId}: {reading.Celsius}°C");
});

// Publish
tempEvent.Publish(new TemperatureReading("Sensor-01", 23.5, DateTime.UtcNow));

// Unsubscribe
subscription.Dispose();
```

### Async Usage

```csharp
public record Notification(string Message);

[Observer(typeof(Notification))]
public partial class NotificationSent
{
}

var notif = new NotificationSent();

// Async handler
notif.Subscribe(async n =>
{
    await SendEmailAsync(n.Message);
    await LogToDbAsync(n);
});

// Await all async handlers
await notif.PublishAsync(new Notification("Hello!"));
```

## Configuration Examples

### Threading Policies

```csharp
// Default: Thread-safe with locks
[Observer(typeof(Message), Threading = ObserverThreadingPolicy.Locking)]
public partial class MessageReceived { }

// Single-threaded: No thread safety, maximum performance
[Observer(typeof(UiEvent), Threading = ObserverThreadingPolicy.SingleThreadedFast)]
public partial class UiEventOccurred { }

// Concurrent: Lock-free for high throughput
[Observer(typeof(Metric), Threading = ObserverThreadingPolicy.Concurrent)]
public partial class MetricRecorded { }
```

### Exception Policies

```csharp
// Continue: Fault-tolerant, all handlers run (default)
[Observer(typeof(Event), Exceptions = ObserverExceptionPolicy.Continue)]
public partial class EventOccurred
{
    partial void OnSubscriberError(Exception ex)
    {
        Logger.LogError(ex, "Handler failed");
    }
}

// Stop: Fail-fast, stop on first error
[Observer(typeof(Payment), Exceptions = ObserverExceptionPolicy.Stop)]
public partial class PaymentProcessed { }

// Aggregate: Collect all errors, throw AggregateException
[Observer(typeof(Validation), Exceptions = ObserverExceptionPolicy.Aggregate)]
public partial class ValidationRequested { }
```

### Order Policies

```csharp
// RegistrationOrder: FIFO, deterministic (default)
[Observer(typeof(Event), Order = ObserverOrderPolicy.RegistrationOrder)]
public partial class EventRaised { }

// Undefined: No order guarantee, potential performance benefit
[Observer(typeof(Metric), Order = ObserverOrderPolicy.Undefined)]
public partial class MetricCollected { }
```

## Common Patterns

### 1. Observable Property

```csharp
public record PropertyChanged(string Name, object? Value);

[Observer(typeof(PropertyChanged))]
public partial class PropertyChangedEvent { }

public class ViewModel
{
    private readonly PropertyChangedEvent _propertyChanged = new();
    private string _name = "";

    public IDisposable OnPropertyChanged(Action<PropertyChanged> handler) =>
        _propertyChanged.Subscribe(handler);

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                _propertyChanged.Publish(new PropertyChanged(nameof(Name), value));
            }
        }
    }
}
```

### 2. Event Aggregator

```csharp
public class EventBus
{
    [Observer(typeof(UserLoggedIn))]
    private partial class UserLoggedInEvent { }

    [Observer(typeof(OrderPlaced))]
    private partial class OrderPlacedEvent { }

    private readonly UserLoggedInEvent _userLoggedIn = new();
    private readonly OrderPlacedEvent _orderPlaced = new();

    public IDisposable OnUserLoggedIn(Action<UserLoggedIn> handler) =>
        _userLoggedIn.Subscribe(handler);

    public IDisposable OnOrderPlaced(Action<OrderPlaced> handler) =>
        _orderPlaced.Subscribe(handler);

    public void Publish(UserLoggedIn e) => _userLoggedIn.Publish(e);
    public void Publish(OrderPlaced e) => _orderPlaced.Publish(e);
}
```

### 3. Composite Subscriptions

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
subscriptions.Add(eventA.Subscribe(HandleA));
subscriptions.Add(eventB.Subscribe(HandleB));
subscriptions.Add(eventC.Subscribe(HandleC));

// Unsubscribe all at once
subscriptions.Dispose();
```

## Running All Examples

To run all examples in sequence:

```csharp
public static async Task RunAllExamples()
{
    // Temperature monitoring examples
    TemperatureMonitorDemo.Run();
    MultipleSubscribersDemo.Run();
    SubscriptionLifecycleDemo.Run();

    // Notification system examples
    await AsyncNotificationDemo.RunAsync();
    ExceptionHandlingDemo.Run();
    await MixedHandlersDemo.RunAsync();
    await CancellationDemo.RunAsync();
}
```

## Key Takeaways

1. **Always dispose subscriptions** - Prevents memory leaks
2. **Use immutable payload types** - Records work great
3. **Choose appropriate policies** - Default (Locking + Continue + RegistrationOrder) is good for most cases
4. **Use PublishAsync for async handlers** - `Publish` fires and forgets; `PublishAsync` awaits
5. **Handle errors gracefully** - Implement `OnSubscriberError` with Continue policy
6. **Keep handlers fast** - Offload work to background tasks if needed

## Performance Tips

- **SingleThreadedFast**: Use for UI thread events (20-30% faster than Locking)
- **Concurrent**: Use for high-throughput metrics (better under contention)
- **Locking**: Default choice, good balance of safety and performance
- **Undefined Order**: Slight performance benefit if order doesn't matter
- **ValueTask**: Async handlers use ValueTask for reduced allocations

## See Also

- [Observer Generator Documentation](/docs/generators/observer.md)
- [PatternKit.Generators API Reference](https://patternkit.dev/api)
- [Observer Pattern (Wikipedia)](https://en.wikipedia.org/wiki/Observer_pattern)
