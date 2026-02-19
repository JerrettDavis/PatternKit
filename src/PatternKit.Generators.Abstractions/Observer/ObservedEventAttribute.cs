namespace PatternKit.Generators.Observer;

/// <summary>
/// Marks a property in an <see cref="ObserverHubAttribute"/>-decorated class as an observable event stream.
/// The property must be static, partial, and have a getter only.
/// </summary>
/// <remarks>
/// <para>
/// The generator will create a singleton instance of the event type for this property.
/// </para>
/// <para>
/// Example:
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
