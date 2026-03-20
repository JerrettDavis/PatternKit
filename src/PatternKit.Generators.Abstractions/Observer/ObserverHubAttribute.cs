using System;

namespace PatternKit.Generators.Observer;

/// <summary>
/// Marks a type as an observer event hub that groups multiple event streams.
/// The type must be declared as partial and static.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is reserved for future hub-based generation support.
/// Use this attribute on a static class that will contain multiple <see cref="ObservedEventAttribute"/>
/// properties, each representing a separate event stream.
/// </para>
/// <para>
/// Example (not yet implemented):
/// <code>
/// [ObserverHub]
/// public static partial class SystemEvents
/// {
///     [ObservedEvent]
///     public static partial TemperatureChanged TemperatureChanged { get; }
/// }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ObserverHubAttribute : Attribute
{
}
