namespace PatternKit.Generators.State;

/// <summary>
/// Controls behavior when a state transition guard returns false.
/// </summary>
public enum StateMachineGuardFailurePolicy
{
    /// <summary>
    /// Throw an <see cref="System.InvalidOperationException"/> when a guard fails.
    /// This is the default policy.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Silently ignore the trigger when a guard fails. No state change occurs.
    /// </summary>
    Ignore = 1,

    /// <summary>
    /// Return false from Fire/FireAsync when a guard fails. No state change occurs.
    /// </summary>
    ReturnFalse = 2
}
