using PatternKit.Behavioral.Iterator;

namespace PatternKit.Examples.IteratorDemo;

/// <summary>
/// Demonstrates the Iterator pattern (Flow/SharedFlow) for stream processing pipelines.
/// This example shows a real-time data processing system for IoT sensor data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-world scenario:</b> An IoT platform that processes sensor readings through
/// multiple stages (validation, transformation, aggregation, alerting).
/// </para>
/// <para>
/// <b>Key GoF concepts demonstrated:</b>
/// <list type="bullet">
/// <item>Iterator abstraction (Flow) - traverse without exposing internals</item>
/// <item>Lazy evaluation - only process items as needed</item>
/// <item>Composable transformations (map, filter, flatMap)</item>
/// <item>Forking/sharing streams for parallel processing</item>
/// </list>
/// </para>
/// </remarks>
public static class IteratorDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // Domain Types
    // ─────────────────────────────────────────────────────────────────────────

    public sealed record SensorReading(
        string SensorId,
        string Type,           // "temperature", "humidity", "pressure"
        double Value,
        DateTime Timestamp);

    public sealed record ProcessedReading(
        string SensorId,
        string Type,
        double Value,
        double NormalizedValue,
        string Status);        // "normal", "warning", "critical"

    public sealed record Alert(
        string SensorId,
        string Message,
        string Severity,
        DateTime Timestamp);

    public sealed record AggregatedStats(
        string Type,
        double Min,
        double Max,
        double Average,
        int Count);

    // ─────────────────────────────────────────────────────────────────────────
    // Simulated Sensor Data Source
    // ─────────────────────────────────────────────────────────────────────────

    public static IEnumerable<SensorReading> GenerateSensorReadings(int count = 20)
    {
        var random = new Random(42);
        var sensors = new[] { "sensor-001", "sensor-002", "sensor-003" };
        var types = new[] { "temperature", "humidity", "pressure" };

        for (int i = 0; i < count; i++)
        {
            var sensorId = sensors[random.Next(sensors.Length)];
            var type = types[random.Next(types.Length)];
            var baseValue = type switch
            {
                "temperature" => 20.0 + random.NextDouble() * 30, // 20-50°C
                "humidity" => 30.0 + random.NextDouble() * 60,    // 30-90%
                "pressure" => 980.0 + random.NextDouble() * 60,   // 980-1040 hPa
                _ => 0.0
            };

            // Occasionally generate extreme values
            if (random.NextDouble() < 0.1)
                baseValue *= 1.5; // Spike

            yield return new SensorReading(
                sensorId, type, Math.Round(baseValue, 2),
                DateTime.UtcNow.AddMinutes(-count + i));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Processing Functions
    // ─────────────────────────────────────────────────────────────────────────

    public static bool IsValidReading(SensorReading r) =>
        r.Value > 0 && r.Value < 10000;

    public static double NormalizeValue(string type, double value) =>
        type switch
        {
            "temperature" => (value - 20) / 30, // Normalize to 0-1 range
            "humidity" => value / 100,
            "pressure" => (value - 980) / 60,
            _ => value
        };

    public static string DetermineStatus(string type, double value) =>
        type switch
        {
            "temperature" when value > 45 => "critical",
            "temperature" when value > 35 => "warning",
            "humidity" when value > 85 => "critical",
            "humidity" when value > 70 => "warning",
            "pressure" when value > 1030 || value < 990 => "warning",
            _ => "normal"
        };

    public static ProcessedReading ProcessReading(SensorReading r) => new(
        r.SensorId, r.Type, r.Value,
        NormalizeValue(r.Type, r.Value),
        DetermineStatus(r.Type, r.Value));

    public static Alert CreateAlert(ProcessedReading r) => new(
        r.SensorId,
        $"{r.Type.ToUpperInvariant()} {r.Status}: {r.Value}",
        r.Status,
        DateTime.UtcNow);

    // ─────────────────────────────────────────────────────────────────────────
    // Iterator/Flow Pipelines using PatternKit
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a basic processing pipeline using Flow.
    /// </summary>
    public static Flow<ProcessedReading> CreateProcessingPipeline(IEnumerable<SensorReading> source)
    {
        return Flow<SensorReading>.From(source)
            .Filter(IsValidReading)                    // Validate readings
            .Map(ProcessReading)                       // Transform to processed
            .Filter(r => r.Status != "normal");        // Only non-normal readings
    }

    /// <summary>
    /// Creates a shared flow for forking the stream to multiple consumers.
    /// </summary>
    public static SharedFlow<ProcessedReading> CreateSharedPipeline(IEnumerable<SensorReading> source)
    {
        return Flow<SensorReading>.From(source)
            .Filter(IsValidReading)
            .Map(ProcessReading)
            .Share();  // Convert to SharedFlow for multi-consumer
    }

    /// <summary>
    /// Runs the complete Iterator pattern demonstration.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           ITERATOR PATTERN DEMONSTRATION                      ║");
        Console.WriteLine("║   IoT Sensor Data Processing with Flow Pipelines             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        var sensorData = GenerateSensorReadings(30).ToList();

        // ── Scenario 1: Basic Flow pipeline ──
        Console.WriteLine("▶ Scenario 1: Basic Processing Pipeline");
        Console.WriteLine(new string('─', 50));
        Console.WriteLine("  Pipeline: Source → Validate → Process → Filter(non-normal)\n");

        var basicPipeline = CreateProcessingPipeline(sensorData);
        var abnormalReadings = basicPipeline.ToList();

        Console.WriteLine($"  Total readings: {sensorData.Count}");
        Console.WriteLine($"  Abnormal readings: {abnormalReadings.Count}");
        Console.WriteLine("  Sample abnormal readings:");
        foreach (var r in abnormalReadings.Take(5))
        {
            var icon = r.Status == "critical" ? "🔴" : "🟡";
            Console.WriteLine($"    {icon} {r.SensorId}: {r.Type} = {r.Value} [{r.Status}]");
        }

        // ── Scenario 2: Map/Filter/FlatMap transformations ──
        Console.WriteLine("\n▶ Scenario 2: Composable Transformations");
        Console.WriteLine(new string('─', 50));

        var alerts = Flow<SensorReading>.From(sensorData)
            .Filter(IsValidReading)
            .Map(ProcessReading)
            .Filter(r => r.Status == "critical")
            .Map(CreateAlert)
            .ToList();

        Console.WriteLine($"  Critical alerts generated: {alerts.Count}");
        foreach (var alert in alerts.Take(3))
        {
            Console.WriteLine($"    🚨 [{alert.Severity.ToUpper()}] {alert.SensorId}: {alert.Message}");
        }

        // ── Scenario 3: Grouping and aggregation ──
        Console.WriteLine("\n▶ Scenario 3: Aggregation by Sensor Type");
        Console.WriteLine(new string('─', 50));

        var byType = Flow<SensorReading>.From(sensorData)
            .Filter(IsValidReading)
            .ToList()
            .GroupBy(r => r.Type)
            .Select(g => new AggregatedStats(
                g.Key,
                g.Min(r => r.Value),
                g.Max(r => r.Value),
                Math.Round(g.Average(r => r.Value), 2),
                g.Count()));

        Console.WriteLine("  Statistics by sensor type:");
        foreach (var stats in byType)
        {
            Console.WriteLine($"    📊 {stats.Type,-12}: Min={stats.Min,7:F1} Max={stats.Max,7:F1} Avg={stats.Average,7:F1} (n={stats.Count})");
        }

        // ── Scenario 4: SharedFlow forking ──
        Console.WriteLine("\n▶ Scenario 4: Forked Stream Processing");
        Console.WriteLine(new string('─', 50));
        Console.WriteLine("  Same source forked to: Alerting, Logging, Analytics\n");

        var shared = CreateSharedPipeline(sensorData);

        // Fork 1: Alerting (critical only)
        var alertFork = shared.Fork()
            .Filter(r => r.Status == "critical")
            .ToList();

        // Fork 2: Logging (all processed)
        var logFork = shared.Fork().ToList();

        // Fork 3: Analytics (temperature only)
        var tempFork = shared.Fork()
            .Filter(r => r.Type == "temperature")
            .ToList();

        Console.WriteLine($"  Alert fork (critical): {alertFork.Count} items");
        Console.WriteLine($"  Logging fork (all): {logFork.Count} items");
        Console.WriteLine($"  Analytics fork (temp): {tempFork.Count} items");

        // ── Scenario 5: Lazy evaluation demonstration ──
        Console.WriteLine("\n▶ Scenario 5: Lazy Evaluation");
        Console.WriteLine(new string('─', 50));

        int processedCount = 0;
        var lazyPipeline = Flow<SensorReading>.From(sensorData)
            .Map(r =>
            {
                processedCount++;
                return r;
            })
            .Filter(IsValidReading)
            .Map(ProcessReading)
            .Filter(_ => true); // Pass through but limit via Take below

        Console.WriteLine("  Pipeline defined but not executed yet...");
        Console.WriteLine($"  Items processed so far: {processedCount}");

        // Take only first 3 items
        var firstThree = lazyPipeline.Take(3).ToList();
        Console.WriteLine($"\n  After taking 3 items:");
        Console.WriteLine($"  Items actually processed: {processedCount}");
        Console.WriteLine($"  Items returned: {firstThree.Count}");
        Console.WriteLine("  (Lazy evaluation stopped after finding 3 matching items)");

        // ── Scenario 6: Tee for side effects ──
        Console.WriteLine("\n▶ Scenario 6: Tee for Side Effects (Logging)");
        Console.WriteLine(new string('─', 50));

        var loggedItems = 0;
        var teePipeline = Flow<SensorReading>.From(sensorData.Take(5))
            .Filter(IsValidReading)
            .Map(ProcessReading)
            .Tee(r =>
            {
                loggedItems++;
                Console.WriteLine($"    [LOG] Processing: {r.SensorId} - {r.Type}");
            })
            .Filter(r => r.Status != "normal")
            .ToList();

        Console.WriteLine($"\n  Items logged: {loggedItems}");
        Console.WriteLine($"  Items matching filter: {teePipeline.Count}");

        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Pattern Benefits Demonstrated:");
        Console.WriteLine("  • Lazy evaluation - only process what's needed");
        Console.WriteLine("  • Composable pipelines - chain transformations fluently");
        Console.WriteLine("  • Stream forking - share one source with multiple consumers");
        Console.WriteLine("  • Side effects via Tee - log without breaking the pipeline");
        Console.WriteLine("  • Decoupled traversal - iterate without exposing internals");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }
}
