namespace PatternKit.Generators.Observer;

/// <summary>
/// Marks a type for Observer pattern code generation.
/// The type must be declared as partial (class, struct, record class, or record struct).
/// </summary>
/// <remarks>
/// <para>
/// The generator will produce Subscribe and Publish methods with configurable
/// threading, exception handling, and ordering semantics.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Observer]
/// public partial class TemperatureChanged { }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ObserverAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the threading policy for Subscribe/Unsubscribe/Publish operations.
    /// Default is <see cref="ObserverThreadingPolicy.Locking"/>.
    /// </summary>
    public ObserverThreadingPolicy Threading { get; set; } = ObserverThreadingPolicy.Locking;

    /// <summary>
    /// Gets or sets the exception handling policy during publishing.
    /// Default is <see cref="ObserverExceptionPolicy.Continue"/>.
    /// </summary>
    public ObserverExceptionPolicy Exceptions { get; set; } = ObserverExceptionPolicy.Continue;

    /// <summary>
    /// Gets or sets the invocation order policy for event handlers.
    /// Default is <see cref="ObserverOrderPolicy.RegistrationOrder"/>.
    /// </summary>
    public ObserverOrderPolicy Order { get; set; } = ObserverOrderPolicy.RegistrationOrder;

    /// <summary>
    /// Gets or sets whether to generate async publish methods.
    /// When not explicitly set, async methods are generated if any async handlers are detected.
    /// </summary>
    public bool GenerateAsync { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to force all handlers to be async.
    /// When true, only async Subscribe methods are generated.
    /// Default is false (both sync and async handlers are supported).
    /// </summary>
    public bool ForceAsync { get; set; } = false;
}
