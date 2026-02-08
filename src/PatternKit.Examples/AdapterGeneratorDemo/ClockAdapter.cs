using PatternKit.Generators.Adapter;

namespace PatternKit.Examples.AdapterGeneratorDemo;

// =============================================================================
// Scenario: Adapting a legacy time service to a modern interface
// =============================================================================

/// <summary>
/// Modern clock interface used throughout the application.
/// Provides a clean, testable abstraction for time operations.
/// </summary>
public interface IClock
{
    /// <summary>Gets the current UTC time.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Gets the current local time.</summary>
    DateTimeOffset LocalNow { get; }

    /// <summary>Gets the Unix timestamp in seconds.</summary>
    long UnixTimestamp { get; }

    /// <summary>Delays for the specified duration.</summary>
    ValueTask DelayAsync(TimeSpan duration, CancellationToken ct = default);
}

/// <summary>
/// A legacy clock implementation from an older library.
/// Has different method names and signatures that don't match IClock.
/// </summary>
public sealed class LegacySystemClock
{
    public DateTime GetCurrentTimeUtc() => DateTime.UtcNow;

    public DateTime GetCurrentTimeLocal() => DateTime.Now;

    public int GetUnixTime() => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public Task Sleep(int milliseconds, CancellationToken cancellation)
        => Task.Delay(milliseconds, cancellation);
}

/// <summary>
/// Adapter mappings that bridge LegacySystemClock to IClock.
/// The generator creates a LegacySystemClockToIClockAdapter class.
/// </summary>
[GenerateAdapter(
    Target = typeof(IClock),
    Adaptee = typeof(LegacySystemClock),
    AdapterTypeName = "SystemClockAdapter")]
public static partial class ClockAdapterMappings
{
    [AdapterMap(TargetMember = nameof(IClock.UtcNow))]
    public static DateTimeOffset MapUtcNow(LegacySystemClock adaptee)
        => new(adaptee.GetCurrentTimeUtc(), TimeSpan.Zero);

    [AdapterMap(TargetMember = nameof(IClock.LocalNow))]
    public static DateTimeOffset MapLocalNow(LegacySystemClock adaptee)
        => new(adaptee.GetCurrentTimeLocal(), DateTimeOffset.Now.Offset);

    [AdapterMap(TargetMember = nameof(IClock.UnixTimestamp))]
    public static long MapUnixTimestamp(LegacySystemClock adaptee)
        => adaptee.GetUnixTime();

    [AdapterMap(TargetMember = nameof(IClock.DelayAsync))]
    public static ValueTask MapDelayAsync(LegacySystemClock adaptee, TimeSpan duration, CancellationToken ct)
        => new(adaptee.Sleep((int)duration.TotalMilliseconds, ct));
}
