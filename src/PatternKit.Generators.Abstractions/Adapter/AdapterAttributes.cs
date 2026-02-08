namespace PatternKit.Generators.Adapter;

/// <summary>
/// Marks a static partial class as an adapter mapping host for generating an object adapter.
/// The host class contains mapping methods that define how target contract members
/// delegate to the adaptee type.
/// </summary>
/// <remarks>
/// <para>
/// The Adapter pattern allows incompatible interfaces to work together by wrapping
/// an object (the adaptee) in an adapter that implements the target interface.
/// </para>
/// <para>
/// This generator creates Object Adapters (composition-based), where the adapter
/// holds a reference to the adaptee and delegates calls through mapping methods.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateAdapter(Target = typeof(IClock), Adaptee = typeof(LegacyClock))]
/// public static partial class ClockAdapters
/// {
///     [AdapterMap(TargetMember = nameof(IClock.Now))]
///     public static DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class GenerateAdapterAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the target contract type (interface or abstract class) that the adapter will implement.
    /// Required.
    /// </summary>
    public Type Target { get; set; } = null!;

    /// <summary>
    /// Gets or sets the adaptee type that provides the actual implementation.
    /// Required.
    /// </summary>
    public Type Adaptee { get; set; } = null!;

    /// <summary>
    /// Gets or sets the name of the generated adapter type.
    /// Default: "{AdapteeName}To{TargetName}Adapter" (e.g., "LegacyClockToIClockAdapter").
    /// </summary>
    public string? AdapterTypeName { get; set; }

    /// <summary>
    /// Gets or sets how to handle target members without explicit mappings.
    /// Default: <see cref="AdapterMissingMapPolicy.Error"/> (emit diagnostic).
    /// </summary>
    public AdapterMissingMapPolicy MissingMap { get; set; } = AdapterMissingMapPolicy.Error;

    /// <summary>
    /// Gets or sets whether the generated adapter class should be sealed.
    /// Default: true.
    /// </summary>
    public bool Sealed { get; set; } = true;

    /// <summary>
    /// Gets or sets the namespace for the generated adapter.
    /// Default: same namespace as the mapping host class.
    /// </summary>
    public string? Namespace { get; set; }
}

/// <summary>
/// Marks a method as a mapping for a specific target contract member.
/// The mapping method defines how the target member delegates to the adaptee.
/// </summary>
/// <remarks>
/// <para>
/// The mapping method signature must match the target member:
/// </para>
/// <list type="bullet">
/// <item><description>First parameter must be the adaptee type</description></item>
/// <item><description>Remaining parameters must match the target member's parameters</description></item>
/// <item><description>Return type must be compatible with the target member's return type</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Target: DateTimeOffset IClock.Now { get; }
/// [AdapterMap(TargetMember = nameof(IClock.Now))]
/// public static DateTimeOffset MapNow(LegacyClock adaptee) => adaptee.GetNow();
/// 
/// // Target: ValueTask IClock.Delay(TimeSpan duration, CancellationToken ct)
/// [AdapterMap(TargetMember = nameof(IClock.Delay))]
/// public static ValueTask MapDelay(LegacyClock adaptee, TimeSpan duration, CancellationToken ct)
///     => new(adaptee.SleepAsync((int)duration.TotalMilliseconds, ct));
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AdapterMapAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the target contract member this method maps to.
    /// Required. Use <c>nameof(ITarget.Member)</c> for compile-time safety.
    /// </summary>
    public string TargetMember { get; set; } = null!;
}

/// <summary>
/// Specifies how to handle target contract members without explicit [AdapterMap] mappings.
/// </summary>
public enum AdapterMissingMapPolicy
{
    /// <summary>
    /// Emit a compiler error diagnostic for unmapped members.
    /// This is the recommended default to ensure all contract members are explicitly handled.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Generate a throwing stub that throws <see cref="NotImplementedException"/>.
    /// Useful during incremental development.
    /// </summary>
    ThrowingStub = 1,

    /// <summary>
    /// Silently ignore unmapped members (compilation will fail if target is interface/abstract).
    /// Discouraged: may lead to incomplete implementations.
    /// </summary>
    Ignore = 2
}
