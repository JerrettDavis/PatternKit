using PatternKit.Generators.Singleton;

namespace PatternKit.Examples.Generators.Singleton;

/// <summary>
/// Eager singleton: application-wide clock service.
/// Created immediately when the type is first referenced.
/// The generator produces: <c>public static AppClock Instance { get; } = new AppClock();</c>
/// </summary>
[Singleton]
public partial class AppClock
{
    private AppClock() { }

    public string Now => DateTime.UtcNow.ToString("O");

    public string ServiceId { get; } = Guid.NewGuid().ToString("N")[..8];
}

/// <summary>
/// Lazy thread-safe singleton: expensive configuration loader.
/// Created on first access via <see cref="System.Lazy{T}"/> with ExecutionAndPublication.
/// Uses a factory method to control initialization.
/// </summary>
[Singleton(Mode = SingletonMode.Lazy, InstancePropertyName = "Current")]
public partial class AppConfig
{
    private readonly Dictionary<string, string> _settings;

    private AppConfig(Dictionary<string, string> settings)
    {
        _settings = settings;
    }

    [SingletonFactory]
    private static AppConfig LoadConfig()
    {
        // Simulate loading from an external source
        var settings = new Dictionary<string, string>
        {
            ["AppName"] = "PatternKit Demo",
            ["Version"] = "1.0.0",
            ["Environment"] = "Production"
        };
        return new AppConfig(settings);
    }

    public string Get(string key) =>
        _settings.TryGetValue(key, out var value) ? value : $"(unknown:{key})";

    public IReadOnlyDictionary<string, string> All => _settings;
}

/// <summary>
/// Demonstrates the Singleton generator with two real-world scenarios:
/// an eager application clock and a lazy configuration service.
/// </summary>
public static class SingletonGeneratorDemo
{
    /// <summary>
    /// Runs a demonstration of eager and lazy singleton instances.
    /// </summary>
    public static List<string> Run()
    {
        var log = new List<string>();

        // Eager singleton: AppClock
        var clock1 = AppClock.Instance;
        var clock2 = AppClock.Instance;
        log.Add($"clock1.ServiceId = {clock1.ServiceId}");
        log.Add($"clock2.ServiceId = {clock2.ServiceId}");
        log.Add($"Same instance: {ReferenceEquals(clock1, clock2)}");

        // Lazy singleton: AppConfig
        var config1 = AppConfig.Current;
        var config2 = AppConfig.Current;
        log.Add($"AppName = {config1.Get("AppName")}");
        log.Add($"Version = {config2.Get("Version")}");
        log.Add($"Same instance: {ReferenceEquals(config1, config2)}");
        log.Add($"Settings count: {config1.All.Count}");

        return log;
    }
}
