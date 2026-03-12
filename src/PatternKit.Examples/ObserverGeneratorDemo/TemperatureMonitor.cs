using PatternKit.Generators.Observer;

namespace PatternKit.Examples.ObserverGeneratorDemo;

/// <summary>
/// Temperature reading from a sensor.
/// </summary>
/// <param name="SensorId">Unique identifier of the sensor.</param>
/// <param name="Celsius">Temperature in Celsius.</param>
/// <param name="Timestamp">When the reading was taken.</param>
public record TemperatureReading(string SensorId, double Celsius, DateTime Timestamp);

/// <summary>
/// Temperature alert when temperature exceeds threshold.
/// </summary>
/// <param name="SensorId">Sensor that triggered the alert.</param>
/// <param name="Temperature">The temperature that triggered the alert.</param>
/// <param name="Threshold">The threshold that was exceeded.</param>
public record TemperatureAlert(string SensorId, double Temperature, double Threshold);

/// <summary>
/// Observable event for temperature readings using default configuration.
/// Threading: Locking (thread-safe)
/// Exceptions: Continue (fault-tolerant)
/// Order: RegistrationOrder (FIFO)
/// </summary>
[Observer(typeof(TemperatureReading))]
public partial class TemperatureChanged
{
    // Optional: Log errors from subscribers
    partial void OnSubscriberError(Exception ex)
    {
        Console.WriteLine($"⚠️ Subscriber error: {ex.Message}");
    }
}

/// <summary>
/// Observable event for temperature alerts with custom configuration.
/// Uses Stop exception policy to ensure critical alerts aren't missed.
/// </summary>
[Observer(typeof(TemperatureAlert), 
    Threading = ObserverThreadingPolicy.Locking,
    Exceptions = ObserverExceptionPolicy.Stop,
    Order = ObserverOrderPolicy.RegistrationOrder)]
public partial class TemperatureAlertRaised
{
}

/// <summary>
/// Temperature monitoring system that simulates sensors and raises events.
/// </summary>
public class TemperatureMonitoringSystem
{
    private readonly TemperatureChanged _temperatureChanged = new();
    private readonly TemperatureAlertRaised _alertRaised = new();
    private readonly Dictionary<string, double> _thresholds = new();
    private readonly Random _random = new();

    /// <summary>
    /// Sets the alert threshold for a specific sensor.
    /// </summary>
    public void SetThreshold(string sensorId, double thresholdCelsius)
    {
        _thresholds[sensorId] = thresholdCelsius;
    }

    /// <summary>
    /// Subscribes to temperature change events.
    /// </summary>
    public IDisposable OnTemperatureChanged(Action<TemperatureReading> handler) =>
        _temperatureChanged.Subscribe(handler);

    /// <summary>
    /// Subscribes to temperature alert events.
    /// </summary>
    public IDisposable OnTemperatureAlert(Action<TemperatureAlert> handler) =>
        _alertRaised.Subscribe(handler);

    /// <summary>
    /// Simulates a sensor reading and publishes events.
    /// </summary>
    public void SimulateReading(string sensorId)
    {
        // Generate random temperature between 15°C and 35°C
        var temperature = 15 + (_random.NextDouble() * 20);
        var reading = new TemperatureReading(sensorId, temperature, DateTime.UtcNow);

        // Publish temperature change
        _temperatureChanged.Publish(reading);

        // Check threshold and raise alert if exceeded
        if (_thresholds.TryGetValue(sensorId, out var threshold) && temperature > threshold)
        {
            var alert = new TemperatureAlert(sensorId, temperature, threshold);
            _alertRaised.Publish(alert);
        }
    }
}

/// <summary>
/// Demonstrates basic Observer pattern usage with temperature monitoring.
/// </summary>
public static class TemperatureMonitorDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Temperature Monitoring System ===\n");

        var system = new TemperatureMonitoringSystem();

        // Configure thresholds
        system.SetThreshold("Sensor-01", 28.0);
        system.SetThreshold("Sensor-02", 25.0);

        // Subscribe to temperature changes
        using var tempSubscription = system.OnTemperatureChanged(reading =>
        {
            Console.WriteLine($"📊 {reading.SensorId}: {reading.Celsius:F1}°C at {reading.Timestamp:HH:mm:ss}");
        });

        // Subscribe to alerts with critical handler
        using var alertSubscription = system.OnTemperatureAlert(alert =>
        {
            Console.WriteLine($"🔥 ALERT! {alert.SensorId} exceeded {alert.Threshold:F1}°C: {alert.Temperature:F1}°C");
        });

        // Additional alert handler for logging
        using var logSubscription = system.OnTemperatureAlert(alert =>
        {
            LogToFile($"Temperature alert: {alert.SensorId} - {alert.Temperature:F1}°C");
        });

        Console.WriteLine("Simulating sensor readings...\n");

        // Simulate readings from multiple sensors
        for (int i = 0; i < 10; i++)
        {
            system.SimulateReading("Sensor-01");
            system.SimulateReading("Sensor-02");
            Thread.Sleep(500); // Simulate time between readings
        }

        Console.WriteLine("\n--- End of simulation ---");
    }

    private static void LogToFile(string message)
    {
        // In real application, write to file
        Console.WriteLine($"  📝 Logged: {message}");
    }
}

/// <summary>
/// Demonstrates multiple subscribers with different behaviors.
/// </summary>
public static class MultipleSubscribersDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== Multiple Subscribers Demo ===\n");

        var temperatureChanged = new TemperatureChanged();

        // Subscriber 1: Console display
        var sub1 = temperatureChanged.Subscribe(reading =>
            Console.WriteLine($"Display: {reading.Celsius:F1}°C"));

        // Subscriber 2: Statistics tracker
        var temperatures = new List<double>();
        var sub2 = temperatureChanged.Subscribe(reading =>
        {
            temperatures.Add(reading.Celsius);
            var avg = temperatures.Average();
            Console.WriteLine($"Stats: Current={reading.Celsius:F1}°C, Avg={avg:F1}°C, Count={temperatures.Count}");
        });

        // Subscriber 3: Faulty handler (demonstrates exception handling)
        var sub3 = temperatureChanged.Subscribe(_ =>
        {
            throw new InvalidOperationException("Simulated error in subscriber");
        });

        // Subscriber 4: Continues to work despite sub3's error
        var sub4 = temperatureChanged.Subscribe(reading =>
            Console.WriteLine($"Monitor: Temperature is {(reading.Celsius > 25 ? "HOT" : "NORMAL")}"));

        Console.WriteLine("Publishing temperature readings...\n");

        temperatureChanged.Publish(new TemperatureReading("Test", 22.5, DateTime.UtcNow));
        Thread.Sleep(100);
        temperatureChanged.Publish(new TemperatureReading("Test", 28.3, DateTime.UtcNow));
        Thread.Sleep(100);
        temperatureChanged.Publish(new TemperatureReading("Test", 24.1, DateTime.UtcNow));

        Console.WriteLine("\nNote: Subscriber 3 threw exceptions, but others continued working.");
        Console.WriteLine("This is because we use ObserverExceptionPolicy.Continue (default).\n");

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
        sub4.Dispose();
    }
}

/// <summary>
/// Demonstrates subscription lifecycle management.
/// </summary>
public static class SubscriptionLifecycleDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== Subscription Lifecycle Demo ===\n");

        var temperatureChanged = new TemperatureChanged();

        Console.WriteLine("1. Creating three subscriptions...");
        var sub1 = temperatureChanged.Subscribe(r => Console.WriteLine($"  Sub1: {r.Celsius:F1}°C"));
        var sub2 = temperatureChanged.Subscribe(r => Console.WriteLine($"  Sub2: {r.Celsius:F1}°C"));
        var sub3 = temperatureChanged.Subscribe(r => Console.WriteLine($"  Sub3: {r.Celsius:F1}°C"));

        Console.WriteLine("\n2. Publishing with all three active:");
        temperatureChanged.Publish(new TemperatureReading("Test", 20.0, DateTime.UtcNow));

        Console.WriteLine("\n3. Disposing sub2...");
        sub2.Dispose();

        Console.WriteLine("\n4. Publishing with sub1 and sub3:");
        temperatureChanged.Publish(new TemperatureReading("Test", 21.0, DateTime.UtcNow));

        Console.WriteLine("\n5. Using 'using' for automatic disposal:");
        using (var tempSub = temperatureChanged.Subscribe(r => Console.WriteLine($"  Temp: {r.Celsius:F1}°C")))
        {
            temperatureChanged.Publish(new TemperatureReading("Test", 22.0, DateTime.UtcNow));
        } // tempSub automatically disposed here

        Console.WriteLine("\n6. After 'using' block (tempSub disposed):");
        temperatureChanged.Publish(new TemperatureReading("Test", 23.0, DateTime.UtcNow));

        // Cleanup
        sub1.Dispose();
        sub3.Dispose();
    }
}
