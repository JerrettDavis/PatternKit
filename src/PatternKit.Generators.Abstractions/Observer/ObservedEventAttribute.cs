using System;

namespace PatternKit.Generators.Observer;

/// <summary>
/// Marks a property in an <see cref="ObserverHubAttribute"/>-decorated class as an observable event stream.
/// The property must be static, partial, and have a getter only.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is reserved for future hub-based generation support.
/// Generators may use this information to provide strongly-typed event infrastructure for the associated event type.
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
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ObservedEventAttribute : Attribute
{
}
