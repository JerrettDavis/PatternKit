namespace PatternKit.Generators.State;

/// <summary>
/// Controls behavior when a trigger is fired that has no valid transition from the current state.
/// </summary>
public enum StateMachineInvalidTriggerPolicy
{
    /// <summary>
    /// Throw an <see cref="System.InvalidOperationException"/> when an invalid trigger is fired.
    /// This is the default policy.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Silently ignore invalid triggers. No state change occurs.
    /// </summary>
    Ignore = 1,

    /// <summary>
    /// Return false from Fire/FireAsync instead of throwing. No state change occurs.
    /// </summary>
    ReturnFalse = 2
}
