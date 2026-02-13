namespace PatternKit.Generators.State;

/// <summary>
/// Marks a method to be invoked during a specific state transition.
/// The method can be synchronous (void/ValueTask) and is executed between
/// exit hooks (old state) and entry hooks (new state).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class StateTransitionAttribute : Attribute
{
    /// <summary>
    /// The state from which this transition originates.
    /// </summary>
    public object From { get; set; } = null!;

    /// <summary>
    /// The trigger that activates this transition.
    /// </summary>
    public object Trigger { get; set; } = null!;

    /// <summary>
    /// The state to which this transition leads.
    /// </summary>
    public object To { get; set; } = null!;
}
