# Iterator / Flow Pattern Real-World Examples

Production-ready examples demonstrating the Iterator/Flow pattern in real-world scenarios.

---

## Example 1: ETL Data Pipeline

### The Problem

An ETL system needs to process large CSV files with validation, transformation, deduplication, and multi-target output (database, analytics, archive).

### The Solution

Use Flow with sharing to process once and output to multiple destinations.

### The Code

```csharp
public class EtlPipeline
{
    private readonly IDatabase _database;
    private readonly IAnalyticsService _analytics;
    private readonly IStorageService _storage;
    private readonly ILogger _logger;

    public EtlResult Process(string csvPath)
    {
        var result = new EtlResult();

        // Parse CSV lazily
        var rawRecords = Flow<string>.From(File.ReadLines(csvPath))
            .Filter(line => !string.IsNullOrWhiteSpace(line))
            .Filter(line => !line.StartsWith("#")) // Skip comments
            .Map(line => ParseCsvLine(line))
            .Tee(r => result.TotalRecords++);

        // Validation pipeline
        var validatedRecords = rawRecords
            .Filter(r =>
            {
                var errors = Validate(r);
                if (errors.Any())
                {
                    result.ValidationErrors.AddRange(errors);
                    return false;
                }
                return true;
            })
            .Tee(r => result.ValidRecords++);

        // Transformation and deduplication
        var processedRecords = validatedRecords
            .Map(r => Normalize(r))
            .Map(r => Enrich(r))
            .DistinctBy(r => r.UniqueKey)
            .Tee(r => result.UniqueRecords++);

        // Share for multiple outputs
        var shared = processedRecords.Share();

        // Output 1: Database
        var dbRecords = shared.Fork()
            .Map(r => ToDbEntity(r))
            .ToList();
        _database.BulkInsert(dbRecords);
        result.DatabaseInserts = dbRecords.Count;

        // Output 2: Analytics (aggregated)
        var analyticsSummary = shared.Fork()
            .Fold(new AnalyticsSummary(), (summary, record) =>
            {
                summary.TotalAmount += record.Amount;
                summary.RecordsByCategory[record.Category] =
                    summary.RecordsByCategory.GetValueOrDefault(record.Category) + 1;
                return summary;
            });
        _analytics.Send(analyticsSummary);

        // Output 3: Archive (gzipped JSON)
        var archiveRecords = shared.Fork().ToList();
        var json = JsonSerializer.Serialize(archiveRecords);
        var compressed = GZip.Compress(json);
        _storage.Upload($"archive/{DateTime.UtcNow:yyyy/MM/dd}/data.json.gz", compressed);
        result.ArchivedRecords = archiveRecords.Count;

        return result;
    }

    private Flow<T> DistinctBy<T, TKey>(Flow<T> flow, Func<T, TKey> keySelector)
    {
        var seen = new HashSet<TKey>();
        return Flow<T>.From(flow.Where(item => seen.Add(keySelector(item))));
    }
}

// Usage
var pipeline = new EtlPipeline(db, analytics, storage, logger);
var result = pipeline.Process("daily_transactions.csv");
Console.WriteLine($"Processed {result.TotalRecords} records:");
Console.WriteLine($"  Valid: {result.ValidRecords}");
Console.WriteLine($"  Unique: {result.UniqueRecords}");
Console.WriteLine($"  DB Inserts: {result.DatabaseInserts}");
```

### Why This Pattern

- **Single pass**: Data flows through once despite multiple outputs
- **Memory efficient**: Streaming, not loading entire file
- **Composable**: Easy to add/remove pipeline stages

---

## Example 2: Log Analysis Dashboard

### The Problem

A monitoring dashboard needs to analyze application logs in real-time, partitioning by severity, calculating statistics, and detecting anomalies.

### The Solution

Use Flow with branching to partition logs and compute multiple metrics simultaneously.

### The Code

```csharp
public class LogAnalyzer
{
    public DashboardData Analyze(IEnumerable<LogEntry> logStream)
    {
        var logs = Flow<LogEntry>.From(logStream)
            .Filter(l => l.Timestamp >= DateTime.UtcNow.AddHours(-1))
            .Share();

        // Partition by severity
        var (errors, nonErrors) = logs.Branch(l => l.Level == LogLevel.Error);
        var (warnings, infoDebug) = nonErrors.Branch(l => l.Level == LogLevel.Warning);
        var (info, debug) = infoDebug.Branch(l => l.Level == LogLevel.Info);

        // Error analysis
        var errorAnalysis = errors.Fork()
            .Fold(new ErrorAnalysis(), (analysis, log) =>
            {
                analysis.Count++;
                var key = $"{log.Source}:{log.Message.GetHashCode()}";
                analysis.ByPattern[key] = analysis.ByPattern.GetValueOrDefault(key) + 1;
                analysis.BySource[log.Source] = analysis.BySource.GetValueOrDefault(log.Source) + 1;
                return analysis;
            });

        // Response time analysis (from Info logs)
        var responseTimeStats = info.Fork()
            .Filter(l => l.Properties.ContainsKey("ResponseTimeMs"))
            .Map(l => l.Properties["ResponseTimeMs"])
            .Fold(new ResponseTimeStats(), (stats, ms) =>
            {
                stats.Count++;
                stats.Total += ms;
                stats.Max = Math.Max(stats.Max, ms);
                stats.Min = Math.Min(stats.Min, ms);
                return stats;
            });

        // Anomaly detection (error rate spike)
        var timeWindows = errors.Fork()
            .Fold(new Dictionary<DateTime, int>(), (windows, log) =>
            {
                var minute = new DateTime(
                    log.Timestamp.Year, log.Timestamp.Month, log.Timestamp.Day,
                    log.Timestamp.Hour, log.Timestamp.Minute, 0);
                windows[minute] = windows.GetValueOrDefault(minute) + 1;
                return windows;
            });

        var anomalies = DetectAnomalies(timeWindows);

        return new DashboardData
        {
            ErrorCount = errorAnalysis.Count,
            WarningCount = warnings.Fold(0, (c, _) => c + 1),
            InfoCount = info.Fold(0, (c, _) => c + 1),
            DebugCount = debug.Fold(0, (c, _) => c + 1),
            TopErrorPatterns = errorAnalysis.ByPattern
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToList(),
            TopErrorSources = errorAnalysis.BySource
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .ToList(),
            AverageResponseTime = responseTimeStats.Count > 0
                ? responseTimeStats.Total / responseTimeStats.Count
                : 0,
            MaxResponseTime = responseTimeStats.Max,
            Anomalies = anomalies
        };
    }
}
```

### Why This Pattern

- **Partitioning**: Branch efficiently splits the stream
- **Multiple aggregations**: Fold computes stats in single pass
- **Composable analysis**: Easy to add new metrics

---

## Example 3: Event Stream Processor

### The Problem

A financial system processes trading events, computing real-time metrics, triggering alerts, and maintaining audit logs - all from the same event stream.

### The Solution

Use Flow with sharing to fan out events to multiple consumers.

### The Code

```csharp
public class TradingEventProcessor
{
    private readonly IAlertService _alerts;
    private readonly IAuditLog _audit;
    private readonly IMetricsService _metrics;
    private readonly IRiskEngine _risk;

    public void ProcessEvents(IEnumerable<TradingEvent> events)
    {
        var shared = Flow<TradingEvent>.From(events)
            .Tee(e => _audit.Log(e))  // Audit all events
            .Share();

        // Branch by event type
        var (trades, nonTrades) = shared.Branch(e => e.Type == EventType.Trade);
        var (orders, cancellations) = nonTrades.Branch(e => e.Type == EventType.Order);

        // Process trades
        ProcessTrades(trades);

        // Process orders
        ProcessOrders(orders);

        // Update cancellation stats
        var cancellationCount = cancellations.Fold(0, (c, _) => c + 1);
        _metrics.Gauge("cancellations.count", cancellationCount);
    }

    private void ProcessTrades(Flow<TradingEvent> trades)
    {
        var shared = trades.Share();

        // Real-time metrics
        var volumeBySymbol = shared.Fork()
            .Fold(new Dictionary<string, decimal>(), (dict, e) =>
            {
                var trade = (TradeEvent)e;
                dict[trade.Symbol] = dict.GetValueOrDefault(trade.Symbol) + trade.Quantity;
                return dict;
            });

        foreach (var (symbol, volume) in volumeBySymbol)
        {
            _metrics.Gauge($"trade.volume.{symbol}", volume);
        }

        // Large trade alerts
        shared.Fork()
            .Filter(e => ((TradeEvent)e).Value > 1_000_000)
            .Tee(e =>
            {
                var trade = (TradeEvent)e;
                _alerts.Send(new LargeTradeAlert
                {
                    Symbol = trade.Symbol,
                    Value = trade.Value,
                    Timestamp = trade.Timestamp
                });
            })
            .ToList(); // Force evaluation

        // Risk calculation
        var positions = shared.Fork()
            .Map(e => (TradeEvent)e)
            .Fold(new Dictionary<string, Position>(), (positions, trade) =>
            {
                if (!positions.TryGetValue(trade.Symbol, out var pos))
                    pos = positions[trade.Symbol] = new Position { Symbol = trade.Symbol };

                pos.Quantity += trade.Side == Side.Buy ? trade.Quantity : -trade.Quantity;
                pos.Value += trade.Side == Side.Buy ? trade.Value : -trade.Value;
                return positions;
            });

        _risk.UpdatePositions(positions.Values);
    }

    private void ProcessOrders(Flow<TradingEvent> orders)
    {
        var orderStats = orders
            .Map(e => (OrderEvent)e)
            .Fold(new OrderStats(), (stats, order) =>
            {
                stats.Count++;
                stats.TotalValue += order.Value;
                stats.ByType[order.OrderType] = stats.ByType.GetValueOrDefault(order.OrderType) + 1;
                return stats;
            });

        _metrics.Gauge("orders.count", orderStats.Count);
        _metrics.Gauge("orders.value", (double)orderStats.TotalValue);
    }
}
```

### Why This Pattern

- **Fan-out processing**: Multiple consumers from single stream
- **Type-safe branching**: Partition by event type
- **Streaming aggregation**: Real-time metrics via Fold

---

## Example 4: Batch Report Generator

### The Problem

A reporting system generates multiple report formats from the same data source with different transformations, filtering, and aggregations for each report type.

### The Solution

Use Flow with forking to generate all reports from a single data pass.

### The Code

```csharp
public class ReportGenerator
{
    public ReportBundle GenerateReports(IEnumerable<SalesRecord> sales, ReportPeriod period)
    {
        var filteredSales = Flow<SalesRecord>.From(sales)
            .Filter(s => s.Date >= period.Start && s.Date <= period.End)
            .Share();

        // Summary Report
        var summaryData = filteredSales.Fork()
            .Fold(new SummaryData(), (summary, sale) =>
            {
                summary.TotalSales += sale.Amount;
                summary.TransactionCount++;
                summary.ByProduct[sale.ProductId] =
                    summary.ByProduct.GetValueOrDefault(sale.ProductId) + sale.Amount;
                summary.ByRegion[sale.Region] =
                    summary.ByRegion.GetValueOrDefault(sale.Region) + sale.Amount;
                return summary;
            });

        var summaryReport = RenderSummaryReport(summaryData, period);

        // Detail Report (sorted, paginated)
        var detailRecords = filteredSales.Fork()
            .Map(s => new DetailRecord
            {
                Date = s.Date,
                Product = s.ProductName,
                Region = s.Region,
                Amount = s.Amount,
                Customer = s.CustomerName
            })
            .ToList()
            .OrderByDescending(r => r.Amount)
            .ToList();

        var detailReport = RenderDetailReport(detailRecords, period);

        // Trend Report (daily aggregates)
        var trendData = filteredSales.Fork()
            .Fold(new Dictionary<DateTime, DailyAggregate>(), (dict, sale) =>
            {
                var day = sale.Date.Date;
                if (!dict.TryGetValue(day, out var agg))
                    dict[day] = agg = new DailyAggregate { Date = day };

                agg.Total += sale.Amount;
                agg.Count++;
                return dict;
            })
            .Values
            .OrderBy(d => d.Date)
            .ToList();

        var trendReport = RenderTrendReport(trendData, period);

        // Regional Breakdown
        var (domestic, international) = filteredSales.Branch(s => s.Region == "Domestic");

        var domesticTotal = domestic.Fold(0m, (sum, s) => sum + s.Amount);
        var internationalTotal = international.Fold(0m, (sum, s) => sum + s.Amount);

        var regionalReport = RenderRegionalReport(domesticTotal, internationalTotal, period);

        return new ReportBundle
        {
            Summary = summaryReport,
            Details = detailReport,
            Trends = trendReport,
            Regional = regionalReport,
            GeneratedAt = DateTime.UtcNow,
            Period = period
        };
    }

    private byte[] RenderSummaryReport(SummaryData data, ReportPeriod period)
    {
        return PdfBuilder.Create()
            .AddTitle($"Sales Summary: {period}")
            .AddSection("Overview")
            .AddKeyValue("Total Sales", data.TotalSales.ToString("C"))
            .AddKeyValue("Transactions", data.TransactionCount.ToString())
            .AddSection("By Product")
            .AddTable(data.ByProduct.Select(kvp => new { Product = kvp.Key, Sales = kvp.Value }))
            .AddSection("By Region")
            .AddChart(ChartType.Pie, data.ByRegion)
            .Build();
    }
}
```

### Why This Pattern

- **Single data pass**: All reports from one database query
- **Parallel processing**: Independent report generation
- **Memory efficient**: Streaming aggregation

---

## Key Takeaways

1. **Share for multiple outputs**: Prevent re-enumeration of expensive sources
2. **Branch for partitioning**: Clean split by predicate
3. **Fold for aggregation**: Efficient single-pass reduction
4. **Tee for side effects**: Logging, auditing without changing flow
5. **Compose operators**: Build complex pipelines from simple parts

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
