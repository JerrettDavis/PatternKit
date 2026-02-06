using PatternKit.Generators.Observer;

namespace PatternKit.Examples.Generators.Observer;

/// <summary>
/// Represents a stock price change event.
/// </summary>
public record StockPriceChanged(string Symbol, decimal Price, DateTime Timestamp);

/// <summary>
/// Generated observer event stream for stock price changes.
/// The [Observer] attribute generates:
/// - Subscribe(Action&lt;StockPriceChanged&gt;) returning IDisposable
/// - Publish(StockPriceChanged) with snapshot semantics
/// </summary>
[Observer(typeof(StockPriceChanged))]
public partial class StockPriceFeed { }

/// <summary>
/// Demonstrates the Observer pattern source generator with a stock price notification scenario.
/// Shows subscription, publishing, unsubscription, and snapshot semantics using generated code.
/// </summary>
public static class ObserverGeneratorDemo
{
    /// <summary>
    /// Runs a demonstration of the observer pattern with subscribe/publish/unsubscribe.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();
        var feed = new StockPriceFeed();

        // Subscribe two observers
        var dashboardSub = feed.Subscribe(e =>
            log.Add($"Dashboard: {e.Symbol} = ${e.Price}"));

        var alertSub = feed.Subscribe(e =>
        {
            if (e.Price > 150m)
                log.Add($"ALERT: {e.Symbol} above $150! Current: ${e.Price}");
        });

        // Publish first price update - both observers receive it
        feed.Publish(new StockPriceChanged("ACME", 145.50m, DateTime.UtcNow));
        log.Add($"After first publish: {log.Count} notifications");

        // Publish second price update - triggers alert
        feed.Publish(new StockPriceChanged("ACME", 155.25m, DateTime.UtcNow));

        // Unsubscribe dashboard
        dashboardSub.Dispose();
        log.Add("Dashboard unsubscribed");

        // Publish third update - only alert subscriber receives
        feed.Publish(new StockPriceChanged("ACME", 160.00m, DateTime.UtcNow));

        // Unsubscribe alert
        alertSub.Dispose();
        log.Add("Alert unsubscribed");

        // Publish fourth update - no subscribers, no notifications
        var countBefore = log.Count;
        feed.Publish(new StockPriceChanged("ACME", 165.00m, DateTime.UtcNow));
        log.Add($"No subscribers: {log.Count == countBefore}");

        return log;
    }
}
