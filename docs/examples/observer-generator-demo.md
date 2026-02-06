# Observer Generator Demo

This demo shows how to use the Observer pattern source generator to build a stock price notification system with zero boilerplate.

## Goal

Create a type-safe, thread-safe event stream where subscribers receive stock price updates and can unsubscribe at any time using IDisposable tokens.

## Key Idea

The `[Observer(typeof(StockPriceChanged))]` attribute generates all the subscription management, snapshot-based iteration, and thread-safe publish infrastructure. You only define the event type and the stream class.

## Code

```csharp
public record StockPriceChanged(string Symbol, decimal Price, DateTime Timestamp);

[Observer(typeof(StockPriceChanged))]
public partial class StockPriceFeed { }
```

Usage:

```csharp
var feed = new StockPriceFeed();

// Subscribe - returns IDisposable
var dashboardSub = feed.Subscribe(e =>
    Console.WriteLine($"Dashboard: {e.Symbol} = ${e.Price}"));

var alertSub = feed.Subscribe(e =>
{
    if (e.Price > 150m)
        Console.WriteLine($"ALERT: {e.Symbol} above $150!");
});

// Publish to all subscribers
feed.Publish(new StockPriceChanged("ACME", 155.25m, DateTime.UtcNow));

// Unsubscribe dashboard - only alerts continue
dashboardSub.Dispose();

feed.Publish(new StockPriceChanged("ACME", 160.00m, DateTime.UtcNow));
```

## Mental Model

Think of the generated observer as a radio station:
- **Subscribe** = tuning in to a frequency
- **Publish** = broadcasting a message to all tuned-in listeners
- **Dispose** = turning off your radio (you stop receiving)
- **Snapshot semantics** = even if someone tunes in during a broadcast, they don't hear the current message; they'll hear the next one

## Test References

- `ObserverGeneratorDemoTests.PublishesToAllSubscribers` - Verifies all subscribers receive events
- `ObserverGeneratorDemoTests.UnsubscribeStopsEvents` - Verifies dispose stops notifications
- `ObserverGeneratorDemoTests.DemoRunsSuccessfully` - Smoke test for the demo

## See Also

- [Observer Generator Reference](../generators/observer.md)
- [Observer In-Process Event Hub (Core Library)](observer-demo.md)
