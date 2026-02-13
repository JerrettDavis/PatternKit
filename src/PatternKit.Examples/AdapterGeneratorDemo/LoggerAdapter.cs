using PatternKit.Generators.Adapter;

namespace PatternKit.Examples.AdapterGeneratorDemo;

// =============================================================================
// Scenario: Adapting a legacy logging library to a modern interface
// =============================================================================

/// <summary>
/// Modern structured logging interface.
/// </summary>
public interface IStructuredLogger
{
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);

    bool IsEnabled(LogLevel level);
}

/// <summary>Log severity levels.</summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// A legacy console logger with a different API.
/// </summary>
public sealed class LegacyConsoleLogger
{
    private readonly string _prefix;
    private readonly int _minimumLevel;

    public LegacyConsoleLogger(string prefix = "LOG", int minimumLevel = 0)
    {
        _prefix = prefix;
        _minimumLevel = minimumLevel;
    }

    public int MinimumLevel => _minimumLevel;

    public void WriteDebug(string msg) => Write(0, "DBG", msg);
    public void WriteInfo(string msg) => Write(1, "INF", msg);
    public void WriteWarning(string msg) => Write(2, "WRN", msg);
    public void WriteError(string msg, Exception? ex = null)
    {
        Write(3, "ERR", msg);
        if (ex != null)
            Console.Error.WriteLine($"[{_prefix}] Exception: {ex.Message}");
    }

    private void Write(int level, string tag, string msg)
    {
        if (level >= _minimumLevel)
            Console.WriteLine($"[{_prefix}:{tag}] {DateTime.UtcNow:HH:mm:ss.fff} {msg}");
    }
}

/// <summary>
/// Adapter mappings that bridge LegacyConsoleLogger to IStructuredLogger.
/// </summary>
[GenerateAdapter(
    Target = typeof(IStructuredLogger),
    Adaptee = typeof(LegacyConsoleLogger),
    AdapterTypeName = "ConsoleLoggerAdapter")]
public static partial class LoggerAdapterMappings
{
    [AdapterMap(TargetMember = nameof(IStructuredLogger.LogDebug))]
    public static void MapLogDebug(LegacyConsoleLogger adaptee, string message)
        => adaptee.WriteDebug(message);

    [AdapterMap(TargetMember = nameof(IStructuredLogger.LogInfo))]
    public static void MapLogInfo(LegacyConsoleLogger adaptee, string message)
        => adaptee.WriteInfo(message);

    [AdapterMap(TargetMember = nameof(IStructuredLogger.LogWarning))]
    public static void MapLogWarning(LegacyConsoleLogger adaptee, string message)
        => adaptee.WriteWarning(message);

    [AdapterMap(TargetMember = nameof(IStructuredLogger.LogError))]
    public static void MapLogError(LegacyConsoleLogger adaptee, string message, Exception? exception)
        => adaptee.WriteError(message, exception);

    [AdapterMap(TargetMember = nameof(IStructuredLogger.IsEnabled))]
    public static bool MapIsEnabled(LegacyConsoleLogger adaptee, LogLevel level)
        => (int)level >= adaptee.MinimumLevel;
}
