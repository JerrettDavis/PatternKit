using PatternKit.Generators.Singleton;

namespace PatternKit.Examples.SingletonGeneratorDemo;

/// <summary>
/// Configuration manager singleton using lazy initialization with custom factory.
/// Demonstrates real-world pattern for managing application configuration.
/// </summary>
[Singleton(Mode = SingletonMode.Lazy)]
public partial class ConfigManager
{
    /// <summary>Gets the application name.</summary>
    public string AppName { get; }

    /// <summary>Gets the current environment (Development, Staging, Production).</summary>
    public string Environment { get; }

    /// <summary>Gets the database connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Gets whether debug logging is enabled.</summary>
    public bool DebugLogging { get; }

    /// <summary>Gets the configuration load timestamp.</summary>
    public DateTime LoadedAt { get; }

    private ConfigManager(string appName, string environment, string connectionString, bool debugLogging)
    {
        AppName = appName;
        Environment = environment;
        ConnectionString = connectionString;
        DebugLogging = debugLogging;
        LoadedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method that loads configuration from environment variables.
    /// In a real application, this might read from a config file or service.
    /// </summary>
    [SingletonFactory]
    private static ConfigManager Create()
    {
        // In a real app, this would read from appsettings.json, environment, etc.
        var appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? "PatternKitDemo";
        var environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var connectionString = System.Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "Server=localhost;Database=Demo";
        var debugLogging = System.Environment.GetEnvironmentVariable("DEBUG_LOGGING") == "true";

        return new ConfigManager(appName, environment, connectionString, debugLogging);
    }

    /// <summary>
    /// Returns a string representation of the current configuration.
    /// </summary>
    public override string ToString() =>
        $"ConfigManager[App={AppName}, Env={Environment}, Debug={DebugLogging}, LoadedAt={LoadedAt:HH:mm:ss}]";
}
