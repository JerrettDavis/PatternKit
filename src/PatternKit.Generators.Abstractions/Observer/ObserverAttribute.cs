using System;

namespace PatternKit.Generators.Observer;

/// <summary>
/// Marks a partial type as an observer event stream.
/// The generator produces Subscribe, Publish, and PublishAsync methods
/// with IDisposable subscription tokens and snapshot-based iteration.
/// </summary>
/// <remarks>
/// <para>
/// The generated code includes:
/// <list type="bullet">
/// <item>Subscribe(Action&lt;T&gt;) returning IDisposable</item>
/// <item>Subscribe(Func&lt;T, ValueTask&gt;) returning IDisposable (when async is enabled)</item>
/// <item>Publish(T) for synchronous notification</item>
/// <item>PublishAsync(T, CancellationToken) for asynchronous notification</item>
/// </list>
/// </para>
/// <para>
/// Publish uses snapshot semantics: the subscriber list is copied before iteration,
/// so subscriptions/unsubscriptions during publish do not affect the current notification round.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Observer(typeof(OrderEvent))]
/// public partial class OrderEventStream { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ObserverAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="ObserverAttribute"/> with the specified payload type.
    /// </summary>
    /// <param name="payloadType">The type of the event payload published to subscribers.</param>
    public ObserverAttribute(Type payloadType)
    {
        PayloadType = payloadType;
    }

    /// <summary>
    /// Gets the type of the event payload.
    /// </summary>
    public Type PayloadType { get; }

    /// <summary>
    /// Gets or sets the threading policy. Default: <see cref="ObserverThreadingPolicy.Locking"/>.
    /// </summary>
    public ObserverThreadingPolicy Threading { get; set; } = ObserverThreadingPolicy.Locking;

    /// <summary>
    /// Gets or sets the exception policy. Default: <see cref="ObserverExceptionPolicy.Continue"/>.
    /// </summary>
    public ObserverExceptionPolicy Exceptions { get; set; } = ObserverExceptionPolicy.Continue;

    /// <summary>
    /// Gets or sets the subscriber ordering policy. Default: <see cref="ObserverOrderPolicy.RegistrationOrder"/>.
    /// </summary>
    public ObserverOrderPolicy Order { get; set; } = ObserverOrderPolicy.RegistrationOrder;

    /// <summary>
    /// Gets or sets whether to generate async Subscribe/PublishAsync methods.
    /// If not explicitly set, inferred from <see cref="ForceAsync"/>.
    /// </summary>
    public bool GenerateAsync { get; set; }

    /// <summary>
    /// Forces generation of async methods even when not otherwise inferred.
    /// </summary>
    public bool ForceAsync { get; set; }
}
