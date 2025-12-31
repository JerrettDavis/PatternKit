# Observer Pattern Real-World Examples

Production-ready examples demonstrating the Observer pattern in real-world scenarios.

---

## Example 1: Real-Time Stock Price Feed

### The Problem

A trading application needs to broadcast stock price updates to multiple components: UI displays, alert systems, logging, and analytics - each with different filtering needs.

### The Solution

Use Observer with predicate filters to route updates to appropriate subscribers.

### The Code

```csharp
public record PriceUpdate(string Symbol, decimal Price, decimal Change, DateTime Timestamp);

public class StockFeed
{
    private readonly Observer<PriceUpdate> _hub;

    public StockFeed()
    {
        _hub = Observer<PriceUpdate>.Create()
            .OnError((ex, in e) =>
                Log.Error(ex, "Handler failed for {Symbol}", e.Symbol))
            .ThrowAggregate()
            .Build();
    }

    public IDisposable SubscribeAll(Action<PriceUpdate> handler) =>
        _hub.Subscribe((in PriceUpdate p) => handler(p));

    public IDisposable SubscribeSymbol(string symbol, Action<PriceUpdate> handler) =>
        _hub.Subscribe(
            (in PriceUpdate p) => p.Symbol == symbol,
            (in PriceUpdate p) => handler(p));

    public IDisposable SubscribeAlerts(decimal threshold, Action<PriceUpdate> handler) =>
        _hub.Subscribe(
            (in PriceUpdate p) => Math.Abs(p.Change) >= threshold,
            (in PriceUpdate p) => handler(p));

    public void PublishUpdate(PriceUpdate update) => _hub.Publish(update);
}

// Usage
var feed = new StockFeed();

// UI: All updates for watched list
var uiSub = feed.SubscribeAll(p =>
    UpdatePriceDisplay(p.Symbol, p.Price));

// Alert: Big movements only
var alertSub = feed.SubscribeAlerts(5.0m, p =>
    ShowAlert($"{p.Symbol} moved {p.Change:+0.00;-0.00}%!"));

// Logging: AAPL only
var logSub = feed.SubscribeSymbol("AAPL", p =>
    Log.Info("AAPL: {Price}", p.Price));

// Simulate updates
feed.PublishUpdate(new PriceUpdate("AAPL", 175.50m, 2.5m, DateTime.UtcNow));
feed.PublishUpdate(new PriceUpdate("TSLA", 250.00m, -7.0m, DateTime.UtcNow));
```

### Why This Pattern

- **Multiple consumers**: UI, alerts, logging all receive updates
- **Selective delivery**: Each subscriber gets only relevant updates
- **Decoupled**: Feed doesn't know about specific consumers

---

## Example 2: Application Event Bus

### The Problem

A modular application needs components to communicate without direct dependencies. Different modules react to events like user login, settings change, and data updates.

### The Solution

Create a central event bus using Observer for loosely-coupled communication.

### The Code

```csharp
public abstract record AppEvent(DateTime Timestamp);
public record UserLoggedIn(DateTime Timestamp, string UserId, string Name) : AppEvent(Timestamp);
public record SettingsChanged(DateTime Timestamp, string Key, object Value) : AppEvent(Timestamp);
public record DataSynced(DateTime Timestamp, int RecordCount) : AppEvent(Timestamp);

public class EventBus
{
    private readonly Observer<AppEvent> _hub;
    private readonly List<IDisposable> _subscriptions = new();

    public EventBus()
    {
        _hub = Observer<AppEvent>.Create()
            .OnError((ex, in e) =>
                Console.Error.WriteLine($"Event handler failed: {ex.Message}"))
            .SwallowErrors() // Don't let one handler break others
            .Build();
    }

    public void Subscribe<T>(Action<T> handler) where T : AppEvent
    {
        var sub = _hub.Subscribe(
            (in AppEvent e) => e is T,
            (in AppEvent e) => handler((T)e));
        _subscriptions.Add(sub);
    }

    public void Publish(AppEvent @event) => _hub.Publish(@event);

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
    }
}

// Module: Analytics
public class AnalyticsModule
{
    public AnalyticsModule(EventBus bus)
    {
        bus.Subscribe<UserLoggedIn>(e =>
            TrackLogin(e.UserId, e.Timestamp));

        bus.Subscribe<DataSynced>(e =>
            TrackSync(e.RecordCount));
    }
}

// Module: UI
public class UIModule
{
    public UIModule(EventBus bus)
    {
        bus.Subscribe<UserLoggedIn>(e =>
            ShowWelcome(e.Name));

        bus.Subscribe<SettingsChanged>(e =>
            RefreshSettings());
    }
}

// Usage
var bus = new EventBus();
var analytics = new AnalyticsModule(bus);
var ui = new UIModule(bus);

bus.Publish(new UserLoggedIn(DateTime.UtcNow, "user123", "Alice"));
bus.Publish(new SettingsChanged(DateTime.UtcNow, "theme", "dark"));
```

### Why This Pattern

- **Loose coupling**: Modules don't reference each other
- **Extensible**: Add new modules without changing existing ones
- **Type-safe**: Generic subscription with type filtering

---

## Example 3: Cache Invalidation Notifications

### The Problem

A distributed cache needs to notify multiple services when cached data is invalidated so they can refresh their local copies.

### The Solution

Use Observer to broadcast cache invalidation events to interested services.

### The Code

```csharp
public record CacheInvalidation(string CacheKey, string Region, DateTime InvalidatedAt);

public class CacheCoordinator
{
    private readonly Observer<CacheInvalidation> _hub;

    public CacheCoordinator()
    {
        _hub = Observer<CacheInvalidation>.Create()
            .OnError((ex, in e) =>
                Log.Warn(ex, "Cache handler failed for key {Key}", e.CacheKey))
            .SwallowErrors() // Cache failures shouldn't cascade
            .Build();
    }

    public IDisposable SubscribeRegion(string region, Action<CacheInvalidation> handler) =>
        _hub.Subscribe(
            (in CacheInvalidation e) => e.Region == region,
            (in CacheInvalidation e) => handler(e));

    public IDisposable SubscribeAll(Action<CacheInvalidation> handler) =>
        _hub.Subscribe((in CacheInvalidation e) => handler(e));

    public void Invalidate(string key, string region)
    {
        var invalidation = new CacheInvalidation(key, region, DateTime.UtcNow);
        _hub.Publish(invalidation);
    }
}

// Services subscribe to relevant regions
public class UserService
{
    private readonly Dictionary<string, User> _localCache = new();
    private readonly IDisposable _subscription;

    public UserService(CacheCoordinator coordinator)
    {
        _subscription = coordinator.SubscribeRegion("users", inv =>
        {
            _localCache.Remove(inv.CacheKey);
            Log.Debug("Evicted user cache: {Key}", inv.CacheKey);
        });
    }
}

public class ProductService
{
    private readonly Dictionary<string, Product> _localCache = new();
    private readonly IDisposable _subscription;

    public ProductService(CacheCoordinator coordinator)
    {
        _subscription = coordinator.SubscribeRegion("products", inv =>
        {
            _localCache.Remove(inv.CacheKey);
        });
    }
}

// Usage
var coordinator = new CacheCoordinator();
var userService = new UserService(coordinator);
var productService = new ProductService(coordinator);

// When user data changes
coordinator.Invalidate("user:123", "users");
// Only UserService receives this notification
```

### Why This Pattern

- **Region-based filtering**: Services only get relevant invalidations
- **Fault isolation**: Swallow errors prevent cascade failures
- **Central coordination**: One place manages cache invalidation

---

## Example 4: Progress Reporting

### The Problem

A long-running operation needs to report progress to multiple consumers: a progress bar, a log file, and a cancellation checker.

### The Solution

Use Observer to broadcast progress updates to multiple listeners.

### The Code

```csharp
public record ProgressUpdate(
    string Operation,
    int Current,
    int Total,
    string Message,
    bool IsComplete);

public class ProgressReporter
{
    private readonly Observer<ProgressUpdate> _hub;

    public ProgressReporter()
    {
        _hub = Observer<ProgressUpdate>.Create()
            .OnError((ex, in p) => Console.Error.WriteLine($"Progress handler error: {ex.Message}"))
            .SwallowErrors()
            .Build();
    }

    public IDisposable Subscribe(Action<ProgressUpdate> handler) =>
        _hub.Subscribe((in ProgressUpdate p) => handler(p));

    public void Report(string operation, int current, int total, string message = "") =>
        _hub.Publish(new ProgressUpdate(operation, current, total, message, current >= total));
}

public class FileProcessor
{
    private readonly ProgressReporter _progress = new();

    public IDisposable OnProgress(Action<ProgressUpdate> handler) =>
        _progress.Subscribe(handler);

    public async Task ProcessFilesAsync(string[] files, CancellationToken ct)
    {
        for (int i = 0; i < files.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            _progress.Report("Processing files", i, files.Length, $"Processing {files[i]}");

            await ProcessFileAsync(files[i], ct);
        }

        _progress.Report("Processing files", files.Length, files.Length, "Complete");
    }
}

// Usage
var processor = new FileProcessor();

// UI progress bar
var progressSub = processor.OnProgress(p =>
{
    var percent = (int)((double)p.Current / p.Total * 100);
    UpdateProgressBar(percent);
});

// Log file
var logSub = processor.OnProgress(p =>
{
    if (p.IsComplete)
        Log.Info("Operation complete: {Op}", p.Operation);
    else if (p.Current % 10 == 0) // Log every 10th item
        Log.Debug("{Op}: {Current}/{Total}", p.Operation, p.Current, p.Total);
});

// Console output
var consoleSub = processor.OnProgress(p =>
    Console.WriteLine($"\r[{p.Current}/{p.Total}] {p.Message}"));

await processor.ProcessFilesAsync(files, cancellationToken);
```

### Why This Pattern

- **Multiple displays**: Progress bar, log, console all update
- **Non-blocking**: Reporting doesn't slow down processing
- **Flexible filtering**: Each subscriber can filter/sample as needed

---

## Key Takeaways

1. **SwallowErrors for resilience**: Don't let one subscriber break others
2. **Predicate filters**: Deliver only relevant events to each subscriber
3. **Dispose subscriptions**: Prevent memory leaks
4. **Thread-safe by design**: Safe for concurrent publish/subscribe
5. **In-process only**: For cross-process, use message queues

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
