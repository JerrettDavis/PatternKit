namespace PatternKit.Generators.Observer;

/// <summary>
/// Controls the order in which subscribers are notified during publish.
/// </summary>
public enum ObserverOrderPolicy
{
    /// <summary>
    /// Subscribers are notified in the order they were registered.
    /// This is the default policy.
    /// </summary>
    RegistrationOrder = 0,

    /// <summary>
    /// No ordering guarantee. The runtime may choose any order for performance.
    /// </summary>
    Undefined = 1
}
