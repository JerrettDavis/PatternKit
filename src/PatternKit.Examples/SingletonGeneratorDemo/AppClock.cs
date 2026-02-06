using PatternKit.Generators.Singleton;

namespace PatternKit.Examples.SingletonGeneratorDemo;

/// <summary>
/// Application clock singleton using eager initialization.
/// Provides a consistent time source throughout the application,
/// which is especially useful for testing (can be mocked).
/// </summary>
[Singleton] // Defaults to eager initialization
public partial class AppClock
{
    /// <summary>Gets the current UTC time.</summary>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>Gets the current local time.</summary>
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <summary>Gets the current date (UTC).</summary>
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Gets the Unix timestamp in seconds.</summary>
    public long UnixTimestamp => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private AppClock() { }
}
