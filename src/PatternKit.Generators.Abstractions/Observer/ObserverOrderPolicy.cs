namespace PatternKit.Generators.Observer;

/// <summary>
/// Defines the invocation order guarantee for event handlers.
/// </summary>
public enum ObserverOrderPolicy
{
    /// <summary>
    /// Handlers are invoked in the order they were registered (FIFO).
    /// Default and recommended for deterministic behavior.
    /// </summary>
    RegistrationOrder = 0,

    /// <summary>
    /// No order guarantee. Handlers may be invoked in any order.
    /// May provide better performance with certain threading policies.
    /// </summary>
    Undefined = 1
}
